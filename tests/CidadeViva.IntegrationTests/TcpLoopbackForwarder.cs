using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace CidadeViva.IntegrationTests;

/// <summary>
/// Encaminhador TCP local (127.0.0.1) para os servicos reais PostgreSQL e Mosquitto,
/// permitindo simular queda e recuperacao de rede/servico nos testes de integracao
/// mantendo autenticacao e protocolo reais contra os containers ativos.
/// </summary>
internal sealed class TcpLoopbackForwarder : IAsyncDisposable
{
    private readonly string _targetHost;
    private readonly int _targetPort;
    private readonly ConcurrentDictionary<Guid, (TcpClient Client, TcpClient Upstream)> _activeConnections = new();
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoopTask;
    private volatile bool _isListening;

    public TcpLoopbackForwarder(string targetHost, int targetPort)
    {
        _targetHost = targetHost;
        _targetPort = targetPort;
        ListenPort = AllocateEphemeralPort();
    }

    public int ListenPort { get; }

    public void Start()
    {
        if (_isListening)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Loopback, ListenPort);
        _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _listener.Start();
        _isListening = true;
        _acceptLoopTask = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    public async Task StopAsync()
    {
        if (!_isListening)
        {
            return;
        }

        _isListening = false;
        if (_cts is not null)
        {
            await _cts.CancelAsync().ConfigureAwait(false);
        }

        _listener?.Stop();

        foreach (KeyValuePair<Guid, (TcpClient Client, TcpClient Upstream)> pair in _activeConnections)
        {
            if (_activeConnections.TryRemove(pair.Key, out (TcpClient Client, TcpClient Upstream) sockets))
            {
                CloseQuietly(sockets.Client);
                CloseQuietly(sockets.Upstream);
            }
        }

        if (_acceptLoopTask is not null)
        {
            try
            {
                await _acceptLoopTask.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Ignorado no encerramento
            }
        }

        _cts?.Dispose();
        _cts = null;
        _listener = null;
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

    private static int AllocateEphemeralPort()
    {
        using var temp = new TcpListener(IPAddress.Loopback, 0);
        temp.Start();
        int port = ((IPEndPoint)temp.LocalEndpoint).Port;
        temp.Stop();
        return port;
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener is not null)
        {
            TcpClient? incoming = null;
            try
            {
                incoming = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                _ = Task.Run(() => HandleConnectionAsync(incoming, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                CloseQuietly(incoming);
                break;
            }
            catch (SocketException) when (!_isListening)
            {
                CloseQuietly(incoming);
                break;
            }
            catch (ObjectDisposedException)
            {
                CloseQuietly(incoming);
                break;
            }
            catch (Exception)
            {
                CloseQuietly(incoming);
            }
        }
    }

    private async Task HandleConnectionAsync(TcpClient client, CancellationToken cancellationToken)
    {
        var upstream = new TcpClient();
        var id = Guid.NewGuid();
        _activeConnections[id] = (client, upstream);

        try
        {
            await upstream.ConnectAsync(_targetHost, _targetPort, cancellationToken).ConfigureAwait(false);

            await using NetworkStream clientStream = client.GetStream();
            await using NetworkStream upstreamStream = upstream.GetStream();

            Task c2u = clientStream.CopyToAsync(upstreamStream, cancellationToken);
            Task u2c = upstreamStream.CopyToAsync(clientStream, cancellationToken);

            await Task.WhenAny(c2u, u2c).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Conexao encerrada ou recusada durante simulacao de falha
        }
        finally
        {
            _activeConnections.TryRemove(id, out _);
            CloseQuietly(client);
            CloseQuietly(upstream);
        }
    }

    private static void CloseQuietly(TcpClient? tcpClient)
    {
        if (tcpClient is null)
        {
            return;
        }

        try
        {
            tcpClient.Client?.Close(0);
        }
        catch (Exception)
        {
            // Ignorado
        }

        try
        {
            tcpClient.Dispose();
        }
        catch (Exception)
        {
            // Ignorado
        }
    }
}
