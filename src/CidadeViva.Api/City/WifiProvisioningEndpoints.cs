using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CidadeViva.Application.City;

namespace CidadeViva.Api.City;

public sealed record WifiNetworkDto(
    string Ssid,
    int Rssi,
    int SignalPercent,
    int Channel,
    string Band,
    string Auth,
    bool IsCurrentNotebookNetwork,
    string Source);

public sealed record Esp32PortStatusDto(
    string Port,
    string DeviceId,
    string Phase,
    bool Connected,
    string Ssid,
    string Ip,
    int Rssi,
    string Transport);

public sealed record WifiProvisionOverviewDto(
    string? NotebookSsid,
    string? NotebookBand,
    int? NotebookSignalPercent,
    IReadOnlyList<WifiNetworkDto> Networks,
    IReadOnlyList<Esp32PortStatusDto> DetectedBoards,
    DateTimeOffset ScannedAt);

public sealed record WifiConnectRequest(
    string Ssid,
    string? Password,
    string? TargetPort,
    string? TargetHost,
    string? DeviceKey);

public sealed record WifiConnectResultDto(
    bool Connected,
    string Target,
    string Ssid,
    string? Ip,
    string Message,
    DateTimeOffset CompletedAt);

public sealed partial class WifiProvisioningEndpoints(IHttpClientFactory httpFactory, IWebHostEnvironment env, TimeProvider clock)
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _knownBoardHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["esp32-energy"] = "192.168.1.6"
    };

    public async Task<WifiProvisionOverviewDto> ScanAsync(string? port, string? host, CancellationToken ct)
    {
        (string? notebookSsid, string? notebookBand, int? notebookSignal) = await ReadNotebookConnectedWifiAsync(ct).ConfigureAwait(false);
        List<WifiNetworkDto> hostNetworks = await ScanWindowsWlanNetworksAsync(notebookSsid, ct).ConfigureAwait(false);
        List<Esp32PortStatusDto> boards = await DetectBoardsAsync(port, host, ct).ConfigureAwait(false);

        string? boardPortToScan = !string.IsNullOrWhiteSpace(port)
            ? port.Trim()
            : boards.FirstOrDefault(b => string.Equals(b.Transport, "USB_SERIAL", StringComparison.Ordinal))?.Port;

        if (!string.IsNullOrWhiteSpace(boardPortToScan))
        {
            IReadOnlyList<WifiNetworkDto> boardNetworks = await ScanViaSerialBoardAsync(boardPortToScan, notebookSsid, ct).ConfigureAwait(false);
            foreach (WifiNetworkDto bn in boardNetworks)
            {
                int existingIdx = hostNetworks.FindIndex(n => string.Equals(n.Ssid, bn.Ssid, StringComparison.Ordinal));
                if (existingIdx >= 0)
                {
                    hostNetworks[existingIdx] = bn;
                }
                else
                {
                    hostNetworks.Add(bn);
                }
            }
        }
        else
        {
            Esp32PortStatusDto? wifiBoard = boards.FirstOrDefault(b => string.Equals(b.Transport, "HTTP_WIFI", StringComparison.Ordinal));
            string? hostToScan = !string.IsNullOrWhiteSpace(host) ? host.Trim() : wifiBoard?.Port;
            if (!string.IsNullOrWhiteSpace(hostToScan))
            {
                IReadOnlyList<WifiNetworkDto> httpNetworks = await ScanViaHttpBoardAsync(hostToScan, notebookSsid, ct).ConfigureAwait(false);
                foreach (WifiNetworkDto hn in httpNetworks)
                {
                    int existingIdx = hostNetworks.FindIndex(n => string.Equals(n.Ssid, hn.Ssid, StringComparison.Ordinal));
                    if (existingIdx >= 0)
                    {
                        hostNetworks[existingIdx] = hn;
                    }
                    else
                    {
                        hostNetworks.Add(hn);
                    }
                }
            }
        }

        List<WifiNetworkDto> sorted = hostNetworks
            .OrderByDescending(n => n.IsCurrentNotebookNetwork)
            .ThenByDescending(n => string.Equals(n.Band, "2,4 GHz", StringComparison.Ordinal))
            .ThenByDescending(n => n.SignalPercent)
            .ToList();

        return new WifiProvisionOverviewDto(
            notebookSsid,
            notebookBand,
            notebookSignal,
            sorted,
            boards,
            clock.GetUtcNow());
    }

    public async Task<WifiConnectResultDto> ConnectAsync(WifiConnectRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        string ssid = request.Ssid?.Trim() ?? string.Empty;
        string password = request.Password ?? string.Empty;

        int ssidByteCount = Encoding.UTF8.GetByteCount(ssid);
        int passByteCount = Encoding.UTF8.GetByteCount(password);
        if (ssidByteCount is < 1 or > 32 || (passByteCount != 0 && passByteCount is < 8 or > 63))
        {
            throw new CityRequestException(400, "INVALID_WIFI_CREDENTIALS");
        }

        if (!string.IsNullOrWhiteSpace(request.TargetHost))
        {
            return await ConnectViaHttpBoardAsync(request.TargetHost.Trim(), ssid, password, ct).ConfigureAwait(false);
        }

        string? targetPort = request.TargetPort?.Trim();
        if (string.IsNullOrWhiteSpace(targetPort))
        {
            List<Esp32PortStatusDto> boards = await DetectBoardsAsync(null, null, ct).ConfigureAwait(false);
            Esp32PortStatusDto? matched = !string.IsNullOrWhiteSpace(request.DeviceKey)
                ? boards.FirstOrDefault(b => string.Equals(b.DeviceId, request.DeviceKey, StringComparison.OrdinalIgnoreCase)) ?? boards.FirstOrDefault()
                : boards.FirstOrDefault();

            if (matched is not null && string.Equals(matched.Transport, "HTTP_WIFI", StringComparison.Ordinal))
            {
                return await ConnectViaHttpBoardAsync(matched.Port, ssid, password, ct).ConfigureAwait(false);
            }
            targetPort = matched?.Port;
        }

        if (string.IsNullOrWhiteSpace(targetPort))
        {
            return await ConnectViaHttpBoardAsync("192.168.4.1", ssid, password, ct).ConfigureAwait(false);
        }

        return await ConnectViaSerialBoardAsync(targetPort, ssid, password, ct).ConfigureAwait(false);
    }

    private async Task<List<Esp32PortStatusDto>> DetectBoardsAsync(string? explicitPort, string? explicitHost, CancellationToken ct)
    {
        List<Esp32PortStatusDto> list = [];
        HashSet<string> candidatePorts = new(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(explicitPort) && IsValidComPort(explicitPort.Trim()))
        {
            candidatePorts.Add(explicitPort.Trim().ToUpperInvariant());
        }
        else
        {
            foreach (string port in await ListWindowsComPortsAsync(ct).ConfigureAwait(false))
            {
                if (!string.Equals(port, "COM3", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(port, "COM4", StringComparison.OrdinalIgnoreCase))
                {
                    candidatePorts.Add(port.ToUpperInvariant());
                }
            }
        }

        foreach (string portName in candidatePorts)
        {
            Esp32PortStatusDto? status = await ProbeSerialPortAsync(portName, ct).ConfigureAwait(false);
            if (status is not null)
            {
                list.Add(status);
                if (status.Connected && !string.IsNullOrWhiteSpace(status.Ip) && status.Ip != "0.0.0.0")
                {
                    _knownBoardHosts[status.DeviceId] = status.Ip;
                }
            }
        }

        HashSet<string> candidateHosts = new(StringComparer.OrdinalIgnoreCase) { "192.168.4.1" };
        if (!string.IsNullOrWhiteSpace(explicitHost))
        {
            candidateHosts.Add(explicitHost.Trim());
        }
        foreach (string knownIp in _knownBoardHosts.Values)
        {
            candidateHosts.Add(knownIp);
        }

        foreach (string hostToProbe in candidateHosts)
        {
            Esp32PortStatusDto? httpBoard = await ProbeHttpBoardAsync(hostToProbe, ct).ConfigureAwait(false);
            if (httpBoard is not null)
            {
                if (httpBoard.Connected && !string.IsNullOrWhiteSpace(httpBoard.Ip) && httpBoard.Ip != "0.0.0.0")
                {
                    _knownBoardHosts[httpBoard.DeviceId] = httpBoard.Ip;
                }
                if (!list.Any(b => string.Equals(b.DeviceId, httpBoard.DeviceId, StringComparison.OrdinalIgnoreCase) && string.Equals(b.Transport, "USB_SERIAL", StringComparison.Ordinal)))
                {
                    list.Add(httpBoard);
                }
            }
        }

        return list;
    }

    private static async Task<IReadOnlyList<string>> ListWindowsComPortsAsync(CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows()) return [];
        string output = await RunProcessAsync("reg", @"query HKLM\HARDWARE\DEVICEMAP\SERIALCOMM", ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(output)) return [];
        List<string> ports = [];
        foreach (Match match in ComPortRegex().Matches(output))
        {
            ports.Add(match.Value.ToUpperInvariant());
        }
        return ports;
    }

    private async Task<Esp32PortStatusDto?> ProbeSerialPortAsync(string portName, CancellationToken ct)
    {
        if (!IsValidComPort(portName)) return null;
        (string? pythonExe, string? scriptPath) = ResolveFirmwareToolPaths();
        if (pythonExe is null || scriptPath is null) return null;

        string json = await RunProcessAsync(pythonExe, $"\"{scriptPath}\" --port {portName} --status", ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using JsonDocument doc = JsonDocument.Parse(json.Trim());
            JsonElement root = doc.RootElement;
            if (root.TryGetProperty("error", out _)) return null;
            string deviceId = root.TryGetProperty("deviceId", out JsonElement d) ? d.GetString() ?? "esp32-energy" : "esp32-energy";
            string phase = root.TryGetProperty("phase", out JsonElement p) ? p.GetString() ?? "WAITING" : "WAITING";
            bool connected = root.TryGetProperty("connected", out JsonElement c) && c.ValueKind == JsonValueKind.True;
            string ssid = root.TryGetProperty("ssid", out JsonElement s) ? s.GetString() ?? "" : "";
            string ip = root.TryGetProperty("ip", out JsonElement ipEl) ? ipEl.GetString() ?? "0.0.0.0" : "0.0.0.0";
            int rssi = root.TryGetProperty("rssi", out JsonElement r) && r.TryGetInt32(out int rv) ? rv : 0;
            return new Esp32PortStatusDto(portName.ToUpperInvariant(), deviceId, phase, connected, ssid, ip, rssi, "USB_SERIAL");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<Esp32PortStatusDto?> ProbeHttpBoardAsync(string host, CancellationToken ct)
    {
        if (!IsSafeHost(host)) return null;
        try
        {
            using HttpClient client = httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromMilliseconds(1200);
            using HttpResponseMessage response = await client.GetAsync(new Uri($"http://{host}/api/wifi/status"), ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;
            using JsonDocument doc = await response.Content.ReadFromJsonAsync<JsonDocument>(ct).ConfigureAwait(false) ?? throw new JsonException();
            JsonElement root = doc.RootElement;
            string deviceId = root.TryGetProperty("deviceId", out JsonElement d) ? d.GetString() ?? "esp32" : "esp32";
            string phase = root.TryGetProperty("phase", out JsonElement p) ? p.GetString() ?? "WAITING" : "WAITING";
            bool connected = root.TryGetProperty("connected", out JsonElement c) && c.ValueKind == JsonValueKind.True;
            string ssid = root.TryGetProperty("ssid", out JsonElement s) ? s.GetString() ?? "" : "";
            string ip = root.TryGetProperty("ip", out JsonElement ipEl) ? ipEl.GetString() ?? host : host;
            int rssi = root.TryGetProperty("rssi", out JsonElement r) && r.TryGetInt32(out int rv) ? rv : 0;
            return new Esp32PortStatusDto(host, deviceId, phase, connected, ssid, ip, rssi, "HTTP_WIFI");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or UriFormatException)
        {
            return null;
        }
    }

    private async Task<IReadOnlyList<WifiNetworkDto>> ScanViaSerialBoardAsync(string portName, string? notebookSsid, CancellationToken ct)
    {
        if (!IsValidComPort(portName)) return [];
        (string? pythonExe, string? scriptPath) = ResolveFirmwareToolPaths();
        if (pythonExe is null || scriptPath is null) return [];

        string json = await RunProcessAsync(pythonExe, $"\"{scriptPath}\" --port {portName} --scan", ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json)) return [];

        List<WifiNetworkDto> networks = [];
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json.Trim());
            if (doc.RootElement.TryGetProperty("networks", out JsonElement arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in arr.EnumerateArray())
                {
                    string ssid = item.TryGetProperty("ssid", out JsonElement s) ? s.GetString() ?? "" : "";
                    if (string.IsNullOrWhiteSpace(ssid)) continue;
                    int rssi = item.TryGetProperty("rssi", out JsonElement r) && r.TryGetInt32(out int rv) ? rv : -75;
                    int ch = item.TryGetProperty("channel", out JsonElement c) && c.TryGetInt32(out int cv) ? cv : 1;
                    string auth = item.TryGetProperty("auth", out JsonElement a) ? a.GetString() ?? "WPA2" : "WPA2";
                    int pct = Math.Clamp(2 * (rssi + 100), 5, 100);
                    if (!networks.Any(n => string.Equals(n.Ssid, ssid, StringComparison.Ordinal)))
                    {
                        networks.Add(new WifiNetworkDto(
                            ssid,
                            rssi,
                            pct,
                            ch,
                            "2,4 GHz",
                            auth,
                            string.Equals(ssid, notebookSsid, StringComparison.Ordinal),
                            $"ESP32 ({portName.ToUpperInvariant()})"));
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Ignore malformed serial scan output
        }
        return networks;
    }

    private async Task<IReadOnlyList<WifiNetworkDto>> ScanViaHttpBoardAsync(string host, string? notebookSsid, CancellationToken ct)
    {
        if (!IsSafeHost(host)) return [];
        List<WifiNetworkDto> list = [];
        try
        {
            using HttpClient client = httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            using HttpResponseMessage response = await client.GetAsync(new Uri($"http://{host}/api/wifi/scan"), ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return [];
            using JsonDocument doc = await response.Content.ReadFromJsonAsync<JsonDocument>(ct).ConfigureAwait(false) ?? throw new JsonException();
            if (doc.RootElement.TryGetProperty("networks", out JsonElement arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in arr.EnumerateArray())
                {
                    string ssid = item.TryGetProperty("ssid", out JsonElement s) ? s.GetString() ?? "" : "";
                    if (string.IsNullOrWhiteSpace(ssid)) continue;
                    int rssi = item.TryGetProperty("rssi", out JsonElement r) && r.TryGetInt32(out int rv) ? rv : -70;
                    int ch = item.TryGetProperty("channel", out JsonElement c) && c.TryGetInt32(out int cv) ? cv : 1;
                    string auth = item.TryGetProperty("auth", out JsonElement a) ? a.GetString() ?? "WPA2" : "WPA2";
                    int pct = Math.Clamp(2 * (rssi + 100), 5, 100);
                    list.Add(new WifiNetworkDto(
                        ssid,
                        rssi,
                        pct,
                        ch,
                        "2,4 GHz",
                        auth,
                        string.Equals(ssid, notebookSsid, StringComparison.Ordinal),
                        $"ESP32 ({host})"));
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or UriFormatException)
        {
            // Ignore unreachable host
        }
        return list;
    }

    private async Task<WifiConnectResultDto> ConnectViaSerialBoardAsync(string portName, string ssid, string password, CancellationToken ct)
    {
        if (!IsValidComPort(portName))
        {
            throw new CityRequestException(400, "INVALID_SERIAL_PORT");
        }

        (string? pythonExe, string? scriptPath) = ResolveFirmwareToolPaths();
        if (pythonExe is null || scriptPath is null)
        {
            return new WifiConnectResultDto(
                false,
                portName.ToUpperInvariant(),
                ssid,
                null,
                "Ambiente Python do PlatformIO não encontrado para comunicação USB serial.",
                clock.GetUtcNow());
        }

        string escapedSsid = ssid.Replace("\"", "\\\"", StringComparison.Ordinal);
        string escapedPass = password.Replace("\"", "\\\"", StringComparison.Ordinal);
        string json = await RunProcessAsync(
            pythonExe,
            $"\"{scriptPath}\" --port {portName} --ssid \"{escapedSsid}\" --password \"{escapedPass}\"",
            ct).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(json.Trim());
                JsonElement root = doc.RootElement;
                bool connected = root.TryGetProperty("connected", out JsonElement c) && c.ValueKind == JsonValueKind.True;
                string? ip = root.TryGetProperty("ip", out JsonElement ipEl) ? ipEl.GetString() : null;
                string? err = root.TryGetProperty("error", out JsonElement errEl) ? errEl.GetString() : null;

                if (connected && !string.IsNullOrWhiteSpace(ip))
                {
                    _knownBoardHosts[portName.ToUpperInvariant()] = ip;
                    return new WifiConnectResultDto(
                        true,
                        portName.ToUpperInvariant(),
                        ssid,
                        ip,
                        $"ESP32 conectado ao Wi-Fi '{ssid}' na mesma rede! IP: {ip}",
                        clock.GetUtcNow());
                }

                string friendly = err switch
                {
                    "BOARD_NOT_RESPONDING" => $"Perfil de teste na porta {portName.ToUpperInvariant()} não respondeu. Pressione o botão EN/RST da placa e tente novamente.",
                    "WIFI_TEST_INVALID_INPUT" or "INVALID_INPUT" => "Credenciais inválidas. Informe um SSID de 1 a 32 bytes e senha de 8 a 63 caracteres.",
                    "WIFI_TEST_TIMEOUT" => "Tempo de conexão esgotado (30s). Confira se a rede é 2,4 GHz e se a senha está correta.",
                    _ => $"Não foi possível conectar na porta {portName.ToUpperInvariant()} ({err ?? "sem confirmação de IP"})."
                };

                return new WifiConnectResultDto(false, portName.ToUpperInvariant(), ssid, null, friendly, clock.GetUtcNow());
            }
            catch (JsonException)
            {
                // Fallthrough to generic error
            }
        }

        return new WifiConnectResultDto(
            false,
            portName.ToUpperInvariant(),
            ssid,
            null,
            $"Sem resposta na porta {portName.ToUpperInvariant()}. Verifique o cabo ou pressione RST/EN na placa.",
            clock.GetUtcNow());
    }

    private async Task<WifiConnectResultDto> ConnectViaHttpBoardAsync(string host, string ssid, string password, CancellationToken ct)
    {
        if (!IsSafeHost(host))
        {
            throw new CityRequestException(400, "INVALID_BOARD_HOST");
        }

        try
        {
            using HttpClient client = httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(8);
            using HttpResponseMessage response = await client.PostAsJsonAsync(
                new Uri($"http://{host}/api/wifi/connect"),
                new { ssid, password },
                ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return new WifiConnectResultDto(
                    false,
                    host,
                    ssid,
                    null,
                    "A placa recusou os parâmetros de Wi-Fi. Verifique o nome da rede e a senha.",
                    clock.GetUtcNow());
            }

            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 25000 && !ct.IsCancellationRequested)
            {
                await Task.Delay(1500, ct).ConfigureAwait(false);
                Esp32PortStatusDto? status = await ProbeHttpBoardAsync(host, ct).ConfigureAwait(false);
                if (status is { Connected: true } && status.Ip != "0.0.0.0")
                {
                    return new WifiConnectResultDto(
                        true,
                        host,
                        ssid,
                        status.Ip,
                        $"ESP32 conectado via rede sem fio ao Wi-Fi '{ssid}'! IP atribuído: {status.Ip}",
                        clock.GetUtcNow());
                }
                if (status is { Phase: "WAITING" } && sw.ElapsedMilliseconds > 6000)
                {
                    break;
                }
            }

            return new WifiConnectResultDto(
                true,
                host,
                ssid,
                null,
                $"Credenciais enviadas para a placa ({host}). A placa está associando à rede '{ssid}'.",
                clock.GetUtcNow());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException)
        {
            return new WifiConnectResultDto(
                false,
                host,
                ssid,
                null,
                $"Não foi possível alcançar a placa em {host} nem em portas USB (COM). Conecte na rede 'CidadeViva-Energy'/'CidadeViva-Water' ou conecte o cabo na COM5.",
                clock.GetUtcNow());
        }
    }

    private (string? PythonExe, string? ScriptPath) ResolveFirmwareToolPaths()
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string pioPython = Path.Combine(userProfile, ".platformio", "penv", "Scripts", "python.exe");
        string pythonExe = File.Exists(pioPython) ? pioPython : "python";

        DirectoryInfo? dir = new(env.ContentRootPath);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "cidade-viva-firmware", "tools", "test-water-wifi.py");
            if (File.Exists(candidate))
            {
                return (pythonExe, candidate);
            }
            dir = dir.Parent;
        }
        return (null, null);
    }

    private static async Task<(string? Ssid, string? Band, int? Signal)> ReadNotebookConnectedWifiAsync(CancellationToken ct)
    {
        string output = await RunProcessAsync("netsh", "wlan show interfaces", ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(output)) return (null, null, null);

        string? ssid = null;
        string? radio = null;
        int? channel = null;
        int? signal = null;

        foreach (string rawLine in output.Split('\n'))
        {
            string line = rawLine.Trim();
            int colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0) continue;
            string key = line[..colon].Trim();
            string val = line[(colon + 1)..].Trim();
            if (string.IsNullOrEmpty(val)) continue;

            if (key.Equals("SSID", StringComparison.OrdinalIgnoreCase))
            {
                ssid = val;
            }
            else if (key.Contains("Radio", StringComparison.OrdinalIgnoreCase) || key.Contains("Rádio", StringComparison.OrdinalIgnoreCase) || key.Contains("Banda", StringComparison.OrdinalIgnoreCase))
            {
                radio = val;
            }
            else if (key.Equals("Channel", StringComparison.OrdinalIgnoreCase) || key.Equals("Canal", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out int ch)) channel = ch;
            }
            else if (key.Equals("Signal", StringComparison.OrdinalIgnoreCase) || key.Equals("Sinal", StringComparison.OrdinalIgnoreCase))
            {
                string num = val.Replace("%", "", StringComparison.Ordinal).Trim();
                if (int.TryParse(num, NumberStyles.Integer, CultureInfo.InvariantCulture, out int sig)) signal = sig;
            }
        }

        string? band = channel is >= 1 and <= 14
            ? "2,4 GHz"
            : channel is > 14
                ? "5 GHz"
                : radio is not null && (radio.Contains("802.11g", StringComparison.OrdinalIgnoreCase) || radio.Contains("802.11b", StringComparison.OrdinalIgnoreCase) || radio.Contains("2,4", StringComparison.OrdinalIgnoreCase) || radio.Contains("2.4", StringComparison.OrdinalIgnoreCase))
                    ? "2,4 GHz"
                    : ssid is not null ? "2,4 / 5 GHz" : null;

        return (ssid, band, signal);
    }

    private static async Task<List<WifiNetworkDto>> ScanWindowsWlanNetworksAsync(string? notebookSsid, CancellationToken ct)
    {
        string output = await RunProcessAsync("netsh", "wlan show networks mode=bssid", ct).ConfigureAwait(false);
        List<WifiNetworkDto> results = [];
        if (string.IsNullOrWhiteSpace(output)) return results;

        string? currentSsid = null;
        string currentAuth = "WPA2";
        int bestSignal = 0;
        int bestChannel = 1;
        bool has24Ghz = false;
        bool has5Ghz = false;

        void FlushCurrent()
        {
            if (string.IsNullOrWhiteSpace(currentSsid)) return;
            int sig = bestSignal > 0 ? bestSignal : 60;
            int rssi = (sig / 2) - 100;
            string band = has24Ghz ? "2,4 GHz" : has5Ghz ? "5 GHz" : bestChannel <= 14 ? "2,4 GHz" : "5 GHz";
            if (!results.Any(r => string.Equals(r.Ssid, currentSsid, StringComparison.Ordinal)))
            {
                results.Add(new WifiNetworkDto(
                    currentSsid,
                    rssi,
                    sig,
                    bestChannel,
                    band,
                    currentAuth,
                    string.Equals(currentSsid, notebookSsid, StringComparison.Ordinal),
                    "Notebook Wi-Fi"));
            }
        }

        foreach (string rawLine in output.Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.StartsWith("SSID ", StringComparison.OrdinalIgnoreCase) && !line.StartsWith("BSSID", StringComparison.OrdinalIgnoreCase))
            {
                FlushCurrent();
                int colon = line.IndexOf(':', StringComparison.Ordinal);
                currentSsid = colon > 0 ? line[(colon + 1)..].Trim() : null;
                currentAuth = "WPA2";
                bestSignal = 0;
                bestChannel = 1;
                has24Ghz = false;
                has5Ghz = false;
                continue;
            }

            int idx = line.IndexOf(':', StringComparison.Ordinal);
            if (idx <= 0 || currentSsid is null) continue;
            string k = line[..idx].Trim();
            string v = line[(idx + 1)..].Trim();

            if (k.Contains("Authentication", StringComparison.OrdinalIgnoreCase) || k.Contains("Autenticação", StringComparison.OrdinalIgnoreCase))
            {
                currentAuth = v.Contains("Open", StringComparison.OrdinalIgnoreCase) || v.Contains("Aberta", StringComparison.OrdinalIgnoreCase)
                    ? "OPEN"
                    : v;
            }
            else if (k.Equals("Signal", StringComparison.OrdinalIgnoreCase) || k.Equals("Sinal", StringComparison.OrdinalIgnoreCase))
            {
                string num = v.Replace("%", "", StringComparison.Ordinal).Trim();
                if (int.TryParse(num, NumberStyles.Integer, CultureInfo.InvariantCulture, out int sig) && sig > bestSignal)
                {
                    bestSignal = sig;
                }
            }
            else if (k.Equals("Channel", StringComparison.OrdinalIgnoreCase) || k.Equals("Canal", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int ch))
                {
                    if (ch is >= 1 and <= 14)
                    {
                        has24Ghz = true;
                        bestChannel = ch;
                    }
                    else if (ch > 14)
                    {
                        has5Ghz = true;
                        if (!has24Ghz) bestChannel = ch;
                    }
                }
            }
            else if (k.Contains("Band", StringComparison.OrdinalIgnoreCase) || k.Contains("Banda", StringComparison.OrdinalIgnoreCase))
            {
                if (v.Contains("2,4", StringComparison.OrdinalIgnoreCase) || v.Contains("2.4", StringComparison.OrdinalIgnoreCase))
                {
                    has24Ghz = true;
                }
                else if (v.Contains('5', StringComparison.OrdinalIgnoreCase))
                {
                    has5Ghz = true;
                }
            }
        }

        FlushCurrent();
        return results;
    }

    private static async Task<string> RunProcessAsync(string fileName, string arguments, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows()) return string.Empty;
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                }
            };
            process.Start();
            string stdout = await process.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            return stdout;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or OperationCanceledException)
        {
            return string.Empty;
        }
    }

    private static bool IsValidComPort(string portName) => ComPortExactRegex().IsMatch(portName);

    private static bool IsSafeHost(string host) => SafeHostRegex().IsMatch(host);

    [GeneratedRegex(@"COM\d+", RegexOptions.IgnoreCase)]
    private static partial Regex ComPortRegex();

    [GeneratedRegex(@"^COM\d+$", RegexOptions.IgnoreCase)]
    private static partial Regex ComPortExactRegex();

    [GeneratedRegex(@"^[a-zA-Z0-9\.\-]+(:\d{1,5})?$")]
    private static partial Regex SafeHostRegex();

    public static void MapWifiEndpoints(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        RouteGroupBuilder read = app.MapGroup("/api/v1/wifi").RequireAuthorization("reader").WithTags("Wi-Fi Provisioning");
        read.MapGet("/scan", async (WifiProvisioningEndpoints wifi, string? port = null, string? host = null, CancellationToken ct = default) =>
            TypedResults.Ok(await wifi.ScanAsync(port, host, ct).ConfigureAwait(false)));

        RouteGroupBuilder write = app.MapGroup("/api/v1/wifi").RequireAuthorization("operator").RequireRateLimiting("write").WithTags("Wi-Fi Provisioning");
        write.MapPost("/connect", async (WifiConnectRequest request, WifiProvisioningEndpoints wifi, CancellationToken ct) =>
            TypedResults.Ok(await wifi.ConnectAsync(request, ct).ConfigureAwait(false)));
    }
}
