using CidadeViva.Application.Abstractions.Health;
using CidadeViva.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using System.Buffers;
using CidadeViva.Application.City;
using CidadeViva.Infrastructure.City;
using Microsoft.Extensions.DependencyInjection;
using MQTTnet.Protocol;

namespace CidadeViva.Infrastructure.Messaging;

/// <summary>
/// Gerencia a sessão autenticada MQTT e reconexão automática para readiness na Entrega B.
/// Não subscreve nem ingere tópicos de telemetria/status/ACK nesta fase.
/// </summary>
public sealed partial class MqttConnectionManager : BackgroundService, IMqttReadinessProbe, ICommandTransport
{
    private readonly IOptions<MqttOptions> _options;
    private readonly ILogger<MqttConnectionManager> _logger;
    private readonly IMqttClient _mqttClient;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private volatile bool _disposed;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly bool _cityEnabled;
    private CancellationToken _stoppingToken;
    public bool IsConnected => !_disposed && _mqttClient.IsConnected;

    public MqttConnectionManager(
        IOptions<MqttOptions> options,
        ILogger<MqttConnectionManager> logger,
        IServiceScopeFactory scopeFactory,
        IOptions<CityRuntimeOptions> cityOptions)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _cityEnabled = cityOptions.Value.Enabled;

        var factory = new MqttClientFactory();
        _mqttClient = factory.CreateMqttClient();
        if (_cityEnabled) _mqttClient.ApplicationMessageReceivedAsync += ReceiveAsync;
    }

    public async Task<bool> CheckReadinessAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return false;
        }

        MqttOptions cfg = _options.Value;
        int timeoutSeconds = Math.Clamp(cfg.ConnectTimeoutSeconds, 1, 15);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        bool lockTaken = false;
        try
        {
            await _connectionLock.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
            lockTaken = true;

            if (_disposed)
            {
                return false;
            }

            if (_mqttClient.IsConnected)
            {
                bool pingSucceeded = await TryPingBrokerAsync(timeoutCts.Token).ConfigureAwait(false);
                if (pingSucceeded)
                {
                    return true;
                }
            }

            return await TryConnectCoreAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                LogMqttReadinessFailed(_logger, ex.GetType().Name);
            }

            return false;
        }
        finally
        {
            if (lockTaken && !_disposed)
            {
                try
                {
                    _connectionLock.Release();
                }
                catch (ObjectDisposedException)
                {
                    // Ignorado durante shutdown
                }
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;
        await EnsureConnectedSafeAsync(stoppingToken).ConfigureAwait(false);

        int delaySeconds = Math.Clamp(_options.Value.ReconnectDelaySeconds, 1, 30);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(delaySeconds));

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await EnsureConnectedSafeAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            return;
        }

        await base.StopAsync(cancellationToken).ConfigureAwait(false);

        if (_disposed)
        {
            return;
        }

        bool lockTaken = false;
        try
        {
            await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            lockTaken = true;

            if (!_disposed && _mqttClient.IsConnected)
            {
                await _mqttClient.DisconnectAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception)
        {
            LogMqttDisconnectWarning(_logger);
        }
        finally
        {
            if (lockTaken && !_disposed)
            {
                try
                {
                    _connectionLock.Release();
                }
                catch (ObjectDisposedException)
                {
                    // Ignorado durante shutdown
                }
            }
        }
    }

    public override void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        base.Dispose();
        _mqttClient.Dispose();
        _connectionLock.Dispose();
    }

    private async Task EnsureConnectedSafeAsync(CancellationToken stoppingToken)
    {
        try
        {
            _ = await CheckReadinessAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Encerramento gracioso solicitado
        }
        catch (Exception)
        {
            LogMqttSupervisionWarning(_logger);
        }
    }

    private async Task<bool> TryPingBrokerAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _mqttClient.TryPingAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task<bool> TryConnectCoreAsync(CancellationToken cancellationToken)
    {
        MqttOptions cfg = _options.Value;
        int timeoutSeconds = Math.Clamp(cfg.ConnectTimeoutSeconds, 1, 15);

        MqttClientOptions clientOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(cfg.Host, cfg.Port)
            .WithClientId(cfg.ClientId)
            .WithCredentials(cfg.Username, cfg.Password)
            .WithCleanSession(!_cityEnabled)
            .WithSessionExpiryInterval(_cityEnabled ? 3600U : 0U)
            .WithTimeout(TimeSpan.FromSeconds(timeoutSeconds))
            .Build();

        try
        {
            MqttClientConnectResult result = await _mqttClient
                .ConnectAsync(clientOptions, cancellationToken)
                .ConfigureAwait(false);

            bool connected = result.ResultCode == MqttClientConnectResultCode.Success && _mqttClient.IsConnected;
            if (connected)
            {
                if (_cityEnabled)
                {
                    MqttClientSubscribeResult subscribed = await _mqttClient.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
                        .WithTopicFilter("cidade-viva/v1/+/telemetry", MqttQualityOfServiceLevel.AtLeastOnce)
                        .WithTopicFilter("cidade-viva/v1/+/status", MqttQualityOfServiceLevel.AtLeastOnce)
                        .WithTopicFilter("cidade-viva/v1/+/ack", MqttQualityOfServiceLevel.AtLeastOnce)
                        .Build(), cancellationToken).ConfigureAwait(false);
                    if (subscribed.Items.Any(item => (int)item.ResultCode > 2))
                    {
                        await _mqttClient.DisconnectAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                        throw new InvalidOperationException("MQTT_SUBSCRIPTION_DENIED");
                    }
                }
                LogMqttConnected(_logger, cfg.ClientId);
            }
            else
            {
                LogMqttConnectionRejected(_logger, result.ResultCode);
            }

            return connected;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                LogMqttConnectException(_logger, ex.GetType().Name);
            }

            return false;
        }
    }

    public async Task PublishAsync(string deviceKey, string json, CancellationToken ct)
    {
        if (!_cityEnabled || !IsConnected) throw new InvalidOperationException("MQTT_UNAVAILABLE");
        MqttClientPublishResult published = await _mqttClient.PublishAsync(new MqttApplicationMessageBuilder()
            .WithTopic($"cidade-viva/v1/{deviceKey}/commands").WithPayload(json)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce).WithRetainFlag(false).Build(), ct).ConfigureAwait(false);
        if (!published.IsSuccess) throw new InvalidOperationException("MQTT_PUBLICATION_DENIED");
    }

    private async Task ReceiveAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        args.AutoAcknowledge = false;
        if (args.ApplicationMessage.Payload.Length > 65536)
        {
            await args.AcknowledgeAsync(_stoppingToken).ConfigureAwait(false);
            return;
        }
        byte[] payload = args.ApplicationMessage.Payload.ToArray();
        while (!_stoppingToken.IsCancellationRequested && _mqttClient.IsConnected)
        {
            try
            {
                await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IDeviceMessageProcessor>()
                    .ProcessAsync(args.ApplicationMessage.Topic, payload, args.ApplicationMessage.Retain, _stoppingToken).ConfigureAwait(false);
                break;
            }
            catch (CityRequestException ex)
            {
                LogRejected(_logger, ex.Message);
                break;
            }
            catch (OperationCanceledException) when (_stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                LogPersistenceFailed(_logger, ex.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(1), _stoppingToken).ConfigureAwait(false);
            }
        }
        if (_stoppingToken.IsCancellationRequested || !_mqttClient.IsConnected)
        {
            args.ProcessingFailed = true;
            return;
        }
        // A disconnected packet context cannot be acknowledged on a new connection.
        // Do not loop on this old context: durable session + message dedup handle re-delivery.
        try { await args.AcknowledgeAsync(_stoppingToken).ConfigureAwait(false); }
        catch (Exception ex) when (ex is not OperationCanceledException || !_stoppingToken.IsCancellationRequested)
        {
            args.ProcessingFailed = true;
            LogPersistenceFailed(_logger, ex.GetType().Name);
        }
    }

    [LoggerMessage(2007, LogLevel.Warning, "Rejected MQTT message: {Code}.")]
    private static partial void LogRejected(ILogger logger, string code);

    [LoggerMessage(2008, LogLevel.Warning, "MQTT persistence failed ({ExceptionType}); retrying without PUBACK.")]
    private static partial void LogPersistenceFailed(ILogger logger, string exceptionType);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "Sessao MQTT autenticada estabelecida para o cliente '{ClientId}'.")]
    private static partial void LogMqttConnected(ILogger logger, string clientId);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Warning,
        Message = "Conexao MQTT recusada pelo broker com codigo '{ResultCode}'.")]
    private static partial void LogMqttConnectionRejected(ILogger logger, MqttClientConnectResultCode resultCode);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Warning,
        Message = "Falha ao conectar no broker MQTT ({ExceptionType}).")]
    private static partial void LogMqttConnectException(ILogger logger, string exceptionType);

    [LoggerMessage(
        EventId = 2004,
        Level = LogLevel.Warning,
        Message = "Verificacao de readiness MQTT falhou ({ExceptionType}).")]
    private static partial void LogMqttReadinessFailed(ILogger logger, string exceptionType);

    [LoggerMessage(
        EventId = 2005,
        Level = LogLevel.Debug,
        Message = "Ciclo de supervisao MQTT encontrou falha de conexao.")]
    private static partial void LogMqttSupervisionWarning(ILogger logger);

    [LoggerMessage(
        EventId = 2006,
        Level = LogLevel.Debug,
        Message = "Aviso ao encerrar sessao MQTT.")]
    private static partial void LogMqttDisconnectWarning(ILogger logger);
}
