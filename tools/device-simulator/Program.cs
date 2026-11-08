using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Protocol;

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; shutdown.Cancel(); };
string envPath = Environment.GetEnvironmentVariable("CITY_ENV_FILE") ?? Path.Combine(Directory.GetCurrentDirectory(), ".env");
Dictionary<string, string> variables = File.Exists(envPath) ? File.ReadAllLines(envPath)
    .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith('#') && l.Contains('=', StringComparison.Ordinal))
    .Select(l => l.Split('=', 2)).ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.Ordinal) : [];
string Get(string key, string? fallback = null) => Environment.GetEnvironmentVariable(key) ?? variables.GetValueOrDefault(key) ?? fallback
    ?? throw new InvalidOperationException($"Configure {key} outside source control.");
string host = Get("MQTT_HOST", "127.0.0.1");
int port = int.Parse(Get("MQTT_PORT", "1883"), System.Globalization.CultureInfo.InvariantCulture);
string username = Get("MQTT_SIMULATOR_USER");
string password = Get("MQTT_SIMULATOR_PASSWORD");
bool lowWater = !args.Contains("--allow-water", StringComparer.Ordinal);
await using var energy = new SimulatedDevice("esp32-energy", host, port, username, password, lowWater);
await using var water = new SimulatedDevice("esp32-water", host, port, username, password, lowWater);
Console.WriteLine("SIMULAÇÃO — two independent MQTT clients; no physical GPIO or flow. Ctrl+C stops. Water low=" + lowWater);
try
{
    await Task.WhenAll(energy.RunAsync(shutdown.Token), water.RunAsync(shutdown.Token));
}
catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { }

internal sealed class SimulatedDevice : IAsyncDisposable
{
    private readonly string _key;
    private readonly IMqttClient _client = new MqttClientFactory().CreateMqttClient();
    private readonly MqttClientOptions _options;
    private readonly Stopwatch _uptime = Stopwatch.StartNew();
    private readonly ConcurrentDictionary<Guid, string> _acks = new();
    private readonly SemaphoreSlim _commandLock = new(1, 1);
    private readonly bool _lowWater;
    private bool _lightOn;
    private string _mode = "MANUAL";
    private DateTimeOffset _pumpUntil = DateTimeOffset.MinValue;
    private DateTimeOffset _cooldownUntil = DateTimeOffset.MinValue;
    private CancellationToken _ct;

    public SimulatedDevice(string key, string host, int port, string username, string password, bool lowWater)
    {
        _key = key; _lowWater = lowWater;
        _options = new MqttClientOptionsBuilder().WithTcpServer(host, port).WithClientId("sim-" + key)
            .WithCredentials(username, password).WithCleanSession()
            .WithWillTopic($"cidade-viva/v1/{key}/status").WithWillPayload(Status(false))
            .WithWillQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce).WithWillRetain().Build();
        _client.ApplicationMessageReceivedAsync += e =>
        {
            string text = e.ApplicationMessage.ConvertPayloadToString();
            bool retained = e.ApplicationMessage.Retain;
            _ = Task.Run(() => HandleCommandAsync(text, retained));
            return Task.CompletedTask;
        };
    }

    public async Task RunAsync(CancellationToken ct)
    {
        _ct = ct;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!_client.IsConnected)
                {
                    await _client.ConnectAsync(_options, ct).ConfigureAwait(false);
                    await _client.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
                        .WithTopicFilter($"cidade-viva/v1/{_key}/commands", MqttQualityOfServiceLevel.AtLeastOnce).Build(), ct).ConfigureAwait(false);
                    await PublishAsync("status", Status(true), true).ConfigureAwait(false);
                }
                string json = JsonSerializer.Serialize(new { schemaVersion = "1.0", messageId = Guid.NewGuid().ToString("N"), deviceId = _key,
                    timestamp = DateTimeOffset.UtcNow, uptimeMs = _uptime.ElapsedMilliseconds, origin = "SIMULATED_INPUT", quality = "VALID", mode = _mode,
                    measurements = _key == "esp32-water" ? (object)new { soilHumidityPct = 37.2, reservoirLow = _lowWater } : new { illuminanceLux = 142.5 },
                    actuators = _key == "esp32-water" ? (object)new { pumpOn = DateTimeOffset.UtcNow < _pumpUntil } : new { mainLightOn = _lightOn } });
                await PublishAsync("telemetry", json, false).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            { Console.WriteLine($"{_key}: reconnect pending ({ex.GetType().Name})."); }
            await Task.Delay(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
        }
    }

    private string Status(bool online) => JsonSerializer.Serialize(new { schemaVersion = "1.0", deviceId = _key,
        online, firmwareVersion = "0.1.0-sim", uptimeMs = _uptime.ElapsedMilliseconds, lastKnownMode = _mode });

    private async Task HandleCommandAsync(string text, bool retained)
    {
        try
        {
            await _commandLock.WaitAsync(_ct).ConfigureAwait(false);
            try
            {
                JsonElement command = JsonSerializer.Deserialize<JsonElement>(text);
                if (retained || command.GetProperty("schemaVersion").GetString() != "1.0" || command.GetProperty("deviceId").GetString() != _key) return;
                Guid id = command.GetProperty("commandId").GetGuid();
                if (_acks.TryGetValue(id, out string? cached)) { await PublishAsync("ack", cached, false).ConfigureAwait(false); return; }
                string status = "APPLIED";
                string? reason = null;
                DateTimeOffset now = DateTimeOffset.UtcNow;
                JsonElement parameters = command.GetProperty("parameters");
                string? action = command.GetProperty("action").GetString();
                if (command.GetProperty("expiresAt").GetDateTimeOffset() <= now) { status = "EXPIRED"; reason = "COMMAND_EXPIRED"; }
                else if (_key == "esp32-water" && action == "PUMP_PULSE")
                {
                    if (_lowWater) { status = "REJECTED"; reason = "RESERVOIR_LOW"; }
                    else if (!parameters.TryGetProperty("durationMs", out JsonElement value) || value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int duration) || duration is < 1 or > 3000)
                    { status = "REJECTED"; reason = "INVALID_DURATION"; }
                    else if (_mode != "MANUAL" || now < _cooldownUntil) { status = "REJECTED"; reason = "COOLDOWN_OR_MODE"; }
                    else { _pumpUntil = now.AddMilliseconds(duration); _cooldownUntil = _pumpUntil.AddSeconds(15); }
                }
                else if ((action == "SET_LIGHT_MODE" && _key == "esp32-energy") || (action == "SET_IRRIGATION_MODE" && _key == "esp32-water"))
                {
                    string? mode = parameters.GetProperty("mode").GetString();
                    if (mode is "AUTO" or "MANUAL") _mode = mode; else { status = "REJECTED"; reason = "INVALID_MODE"; }
                }
                else if (action == "SET_LIGHT_MAIN" && _key == "esp32-energy") _lightOn = parameters.GetProperty("on").GetBoolean();
                else { status = "REJECTED"; reason = "UNSUPPORTED_SIMULATION_ACTION"; }
                string ack = JsonSerializer.Serialize(new { schemaVersion = "1.0", deviceId = _key, commandId = id, status, reason,
                    observedState = _key == "esp32-water" ? (object)new { pumpOn = now < _pumpUntil } : new { mainLightOn = _lightOn }, uptimeMs = _uptime.ElapsedMilliseconds });
                _acks[id] = ack;
                await PublishAsync("ack", ack, false).ConfigureAwait(false);
            }
            finally { _commandLock.Release(); }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !_ct.IsCancellationRequested)
        { Console.WriteLine($"{_key}: command rejected or ack unavailable ({ex.GetType().Name})."); }
        catch (OperationCanceledException) { }
    }

    private async Task PublishAsync(string kind, string json, bool retained) =>
        await _client.PublishAsync(new MqttApplicationMessageBuilder().WithTopic($"cidade-viva/v1/{_key}/{kind}")
            .WithPayload(json).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce).WithRetainFlag(retained).Build(), _ct).ConfigureAwait(false);

    public async ValueTask DisposeAsync()
    {
        if (_client.IsConnected)
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await _client.PublishAsync(new MqttApplicationMessageBuilder().WithTopic($"cidade-viva/v1/{_key}/status")
                    .WithPayload(Status(false)).WithRetainFlag().WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce).Build(), timeout.Token).ConfigureAwait(false);
                await _client.DisconnectAsync(cancellationToken: timeout.Token).ConfigureAwait(false);
            }
            catch (Exception) { /* LWT handles ungraceful disconnect. */ }
        }
        _client.Dispose(); _commandLock.Dispose();
    }
}
