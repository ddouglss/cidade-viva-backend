using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using CidadeViva.Application.City;
using CidadeViva.Infrastructure.City;
using CidadeViva.Infrastructure.Persistence;
using CidadeViva.Domain.City;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MQTTnet;
using MQTTnet.Protocol;
using Npgsql;
using Xunit;

namespace CidadeViva.IntegrationTests;

public sealed class CityMvpIntegrationTests
{
    [Fact]
    public async Task RealBrokerDatabaseApiAndAckWorkflowShouldBeSafeAndIdempotent()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        CancellationToken ct = timeout.Token;
        Dictionary<string, string?> cfg = FoundationHealthIntegrationTests.LoadExternalTestConfig();
        string databaseName = "cv_backend_it_" + Guid.NewGuid().ToString("N");
        string brokerName = "cv-mqtt-it-" + Guid.NewGuid().ToString("N");
        var adminBuilder = new NpgsqlConnectionStringBuilder { Host = cfg["Database:Host"], Port = int.Parse(cfg["Database:Port"]!, System.Globalization.CultureInfo.InvariantCulture),
            Database = "postgres", Username = cfg["Database:Username"], Password = cfg["Database:Password"], Pooling = false };
        await using var admin = new NpgsqlConnection(adminBuilder.ConnectionString);
        await admin.OpenAsync(ct);
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin)) await create.ExecuteNonQueryAsync(ct);
        try
        {
            string root = FindRoot();
            int reservedPort;
            using (var portReservation = new TcpListener(IPAddress.Loopback, 0))
            { portReservation.Start(); reservedPort = ((IPEndPoint)portReservation.LocalEndpoint).Port; }
            await DockerAsync(["run", "--rm", "-d", "--name", brokerName, "-p", $"127.0.0.1:{reservedPort}:1883",
                "-v", Path.Combine(root, "infra/mosquitto/config") + ":/mosquitto/config:ro",
                "-v", Path.Combine(root, "infra/mosquitto/secrets") + ":/mosquitto/secrets:ro", "eclipse-mosquitto:2.0.22-openssl"], ct);
            string portMapping = await DockerAsync(["port", brokerName, "1883/tcp"], ct);
            int port = int.Parse(portMapping.Trim().Split(':')[^1], System.Globalization.CultureInfo.InvariantCulture);
            cfg["Database:Database"] = databaseName;
            cfg["Mqtt:Host"] = "127.0.0.1"; cfg["Mqtt:Port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture);
            cfg["City:Enabled"] = "true"; cfg["City:TelemetryStaleTtlSeconds"] = "2";
            cfg["City:AckTimeoutSeconds"] = "2";
            string password = "IT-" + Guid.NewGuid().ToString("N");
            cfg["Bootstrap:viewer:Username"] = "viewer"; cfg["Bootstrap:viewer:Password"] = password;
            cfg["Bootstrap:operator:Username"] = "operator"; cfg["Bootstrap:operator:Password"] = password;
            await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(cfg)));
            // Initialize explicitly before the MQTT consumer/dispatcher starts.
            await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(ct);
                await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(ct);
            }
            using HttpClient client = factory.CreateClient();
            using HttpResponseMessage anonymous = await client.GetAsync("/api/v1/devices", ct);
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            string viewerToken = await LoginAsync(client, "viewer", password, ct);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", viewerToken);
            using HttpResponseMessage forbidden = await client.PostAsJsonAsync("/api/v1/devices/esp32-water/commands",
                new { action = "PUMP_PULSE", parameters = new { durationMs = 100 } }, ct);
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
            string token = await LoginAsync(client, "operator", password, ct);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var websocketClient = factory.Server.CreateWebSocketClient();
            websocketClient.ConfigureRequest = request => request.Headers.Authorization = "Bearer " + token;
            using WebSocket websocket = await websocketClient.ConnectAsync(new Uri("ws://localhost/hubs/city"), ct);
            await websocket.SendAsync(Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\u001e"), WebSocketMessageType.Text, true, ct);
            byte[] handshake = new byte[1024];
            var handshakeResult = await websocket.ReceiveAsync(handshake, ct);
            Assert.Contains("{}", Encoding.UTF8.GetString(handshake, 0, handshakeResult.Count), StringComparison.Ordinal);
            var devices = await client.GetFromJsonAsync<DeviceDto[]>("/api/v1/devices", ct);
            Assert.Equal(2, devices!.Length);
            Assert.All(devices, d => Assert.True(d.IsStale));

            var mqttFactory = new MqttClientFactory();
            using IMqttClient deviceClient = mqttFactory.CreateMqttClient();
            string? receivedCommand = null;
            deviceClient.ApplicationMessageReceivedAsync += args => { receivedCommand = args.ApplicationMessage.ConvertPayloadToString(); return Task.CompletedTask; };
            // This test client uses backend credentials to observe commands; publications are tested separately below.
            await deviceClient.ConnectAsync(new MqttClientOptionsBuilder().WithTcpServer("127.0.0.1", port)
                .WithClientId("cv-command-observer-" + Guid.NewGuid().ToString("N"))
                .WithCredentials(cfg["Mqtt:Username"], cfg["Mqtt:Password"]).Build(), ct);
            // Backend ACL cannot read commands: use simulator credentials from the same external file without exposing them.
            await deviceClient.DisconnectAsync(cancellationToken: ct);
            DeviceDto waterDevice = (await client.GetFromJsonAsync<DeviceDto>("/api/v1/devices/esp32-water", ct))!;
            RuleDto rule = Assert.Single(waterDevice.AutomationRules);
            using HttpResponseMessage ruleChange = await client.PutAsJsonAsync($"/api/v1/automation-rules/{rule.Id}", new RuleUpdate(50, 15000, rule.Version), ct);
            Assert.Equal(HttpStatusCode.OK, ruleChange.StatusCode);
            RuleDto changedRule = (await ruleChange.Content.ReadFromJsonAsync<RuleDto>(ct))!;
            Assert.Equal("NOT_SENT", changedRule.DeliveryStatus);
            using HttpResponseMessage staleRule = await client.PutAsJsonAsync($"/api/v1/automation-rules/{rule.Id}", new RuleUpdate(50, 15000, rule.Version), ct);
            Assert.Equal(HttpStatusCode.Conflict, staleRule.StatusCode);
            using HttpResponseMessage limited = await client.PostAsJsonAsync("/api/v1/devices/esp32-water/commands", new { action = "PUMP_PULSE", parameters = new { durationMs = 100 } }, ct);
            Assert.Equal(HttpStatusCode.BadRequest, limited.StatusCode);
            using HttpResponseMessage restoreRule = await client.PutAsJsonAsync($"/api/v1/automation-rules/{rule.Id}", new RuleUpdate(3000, 15000, changedRule.Version), ct);
            Assert.Equal(HttpStatusCode.OK, restoreRule.StatusCode);

            var expired = new DeviceCommand { DeviceId = waterDevice.Id, Action = "PUMP_PULSE", ParametersJson = "{\"durationMs\":100}",
                RequestedBy = "test", RequestedAt = DateTimeOffset.UtcNow.AddSeconds(-30), ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-20) };
            var unconfirmed = new DeviceCommand { DeviceId = waterDevice.Id, Action = "PUMP_PULSE", ParametersJson = "{\"durationMs\":100}",
                RequestedBy = "test", Status = "PUBLISHED", RequestedAt = DateTimeOffset.UtcNow.AddSeconds(-30),
                PublishedAt = DateTimeOffset.UtcNow.AddSeconds(-25), ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-20) };
            await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<CidadeVivaDbContext>();
                db.Commands.AddRange(expired, unconfirmed); await db.SaveChangesAsync(ct);
            }
            await EventuallyAsync(async () => (await client.GetFromJsonAsync<CommandDto>($"/api/v1/commands/{expired.Id}", ct))?.Status == "EXPIRED", ct);
            await EventuallyAsync(async () => (await client.GetFromJsonAsync<CommandDto>($"/api/v1/commands/{unconfirmed.Id}", ct))?.Status == "TIMED_OUT", ct);
            Dictionary<string, string> simulatorCfg = ReadSimulatorConfig(root);
            await deviceClient.ConnectAsync(new MqttClientOptionsBuilder().WithTcpServer("127.0.0.1", port)
                .WithClientId("cv-device-it-" + Guid.NewGuid().ToString("N"))
                .WithCredentials(simulatorCfg["MQTT_SIMULATOR_USER"], simulatorCfg["MQTT_SIMULATOR_PASSWORD"]).Build(), ct);
            await deviceClient.SubscribeAsync(new MqttClientSubscribeOptionsBuilder().WithTopicFilter("cidade-viva/v1/esp32-water/commands", MqttQualityOfServiceLevel.AtLeastOnce).Build(), ct);

            string messageId = Guid.NewGuid().ToString("N");
            object payload = new { schemaVersion = "1.0", messageId, deviceId = "esp32-water", uptimeMs = 100,
                origin = "SIMULATED_INPUT", quality = "VALID", mode = "MANUAL",
                measurements = new { soilHumidityPct = 37.2, reservoirLow = true }, actuators = new { pumpOn = false } };
            string invalidJson = JsonSerializer.Serialize(payload).Replace("\"schemaVersion\":\"1.0\"", "\"schemaVersion\":\"2.0\"", StringComparison.Ordinal);
            await deviceClient.PublishAsync(new MqttApplicationMessageBuilder().WithTopic("cidade-viva/v1/esp32-water/telemetry")
                .WithPayload(invalidJson).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce).Build(), ct);
            string mismatched = JsonSerializer.Serialize(payload).Replace("\"deviceId\":\"esp32-water\"", "\"deviceId\":\"esp32-energy\"", StringComparison.Ordinal);
            await deviceClient.PublishAsync(new MqttApplicationMessageBuilder().WithTopic("cidade-viva/v1/esp32-water/telemetry")
                .WithPayload(mismatched).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce).Build(), ct);
            await PublishAsync(deviceClient, "telemetry", payload, ct);
            await ReceiveEventAsync(websocket, "DeviceTelemetryUpdated", ct);
            await EventuallyAsync(async () => (await client.GetFromJsonAsync<PageDto<ReadingDto>>("/api/v1/devices/esp32-water/telemetry", ct))?.TotalCount == 2, ct);
            await PublishAsync(deviceClient, "telemetry", payload, ct);
            var readings = await client.GetFromJsonAsync<PageDto<ReadingDto>>("/api/v1/devices/esp32-water/telemetry", ct);
            Assert.Equal(2, readings!.TotalCount);
            Assert.All(readings.Items, r => { Assert.Equal("SIMULATED_INPUT", r.Origin); Assert.Null(r.ObservedAt); });
            using HttpResponseMessage invalidPage = await client.GetAsync("/api/v1/devices/esp32-water/telemetry?pageSize=201", ct);
            Assert.Equal(HttpStatusCode.BadRequest, invalidPage.StatusCode);

            Guid commandId = Guid.NewGuid();
            var request = new { commandId, action = "PUMP_PULSE", parameters = new { durationMs = 100 } };
            using HttpResponseMessage accepted = await client.PostAsJsonAsync("/api/v1/devices/esp32-water/commands", request, ct);
            Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
            Assert.Equal("PENDING", (await accepted.Content.ReadFromJsonAsync<CommandDto>(ct))!.Status);
            using HttpResponseMessage retry = await client.PostAsJsonAsync("/api/v1/devices/esp32-water/commands", request, ct);
            Assert.Equal(commandId, (await retry.Content.ReadFromJsonAsync<CommandDto>(ct))!.CommandId);
            using HttpResponseMessage conflict = await client.PostAsJsonAsync("/api/v1/devices/esp32-water/commands",
                new { commandId, action = "PUMP_PULSE", parameters = new { durationMs = 200 } }, ct);
            Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
            await EventuallyAsync(() => Task.FromResult(receivedCommand is not null), ct);
            Assert.Equal(commandId, JsonSerializer.Deserialize<JsonElement>(receivedCommand!).GetProperty("commandId").GetGuid());
            object ack = new { schemaVersion = "1.0", deviceId = "esp32-water", commandId, status = "REJECTED", reason = "RESERVOIR_LOW", observedState = new { pumpOn = false }, uptimeMs = 120 };
            await PublishAsync(deviceClient, "ack", ack, ct);
            await EventuallyAsync(async () => (await client.GetFromJsonAsync<CommandDto>($"/api/v1/commands/{commandId}", ct))?.Status == "REJECTED", ct);
            await PublishAsync(deviceClient, "ack", ack, ct);
            var alerts = await client.GetFromJsonAsync<AlertDto[]>("/api/v1/alerts", ct);
            AlertDto alert = Assert.Single(alerts!, a => a.Cause == "RESERVOIR_LOW" && a.Active);
            using HttpResponseMessage acknowledged = await client.PostAsync($"/api/v1/alerts/{alert.Id}/ack", null, ct);
            AlertDto result = (await acknowledged.Content.ReadFromJsonAsync<AlertDto>(ct))!;
            Assert.True(result.Active); Assert.NotNull(result.AcknowledgedAt);
            await EventuallyAsync(async () => (await client.GetFromJsonAsync<DeviceDto>("/api/v1/devices/esp32-water", ct))!.IsStale, ct);
            await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<CidadeVivaDbContext>();
                Assert.Equal(1, await db.Messages.CountAsync(ct));
                Assert.Equal(1, await db.Acknowledgements.CountAsync(ct));
                Assert.Equal(3, await db.Commands.CountAsync(ct));
            }
            await deviceClient.DisconnectAsync(cancellationToken: ct);
            // Exercise the actual simulator executable, including one LWT per independent logical board.
            var simulatorStart = new ProcessStartInfo("dotnet") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (string arg in new[] { "run", "--no-build", "--project", "tools/device-simulator" }) simulatorStart.ArgumentList.Add(arg);
            simulatorStart.Environment["MQTT_HOST"] = "127.0.0.1";
            simulatorStart.Environment["MQTT_PORT"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture);
            simulatorStart.Environment["MQTT_SIMULATOR_USER"] = simulatorCfg["MQTT_SIMULATOR_USER"];
            simulatorStart.Environment["MQTT_SIMULATOR_PASSWORD"] = simulatorCfg["MQTT_SIMULATOR_PASSWORD"];
            using Process simulator = Process.Start(simulatorStart)!;
            try
            {
                await EventuallyAsync(async () => (await client.GetFromJsonAsync<DeviceDto[]>("/api/v1/devices", ct))!.All(d => d.Online && d.Origin == "SIMULATED_INPUT"), ct);
                using HttpResponseMessage simulatedCommand = await client.PostAsJsonAsync("/api/v1/devices/esp32-water/commands",
                    new { action = "PUMP_PULSE", parameters = new { durationMs = 100 } }, ct);
                var simulated = (await simulatedCommand.Content.ReadFromJsonAsync<CommandDto>(ct))!;
                await EventuallyAsync(async () => (await client.GetFromJsonAsync<CommandDto>($"/api/v1/commands/{simulated.CommandId}", ct))?.Reason == "RESERVOIR_LOW", ct);
                DateTimeOffset beforeRestart = DateTimeOffset.UtcNow;
                await DockerAsync(["restart", brokerName], ct);
                await EventuallyAsync(async () => (await client.GetFromJsonAsync<DeviceDto[]>("/api/v1/devices", ct))!
                    .All(d => d.Online && d.LastTelemetryAt > beforeRestart), ct);
            }
            finally
            {
                if (!simulator.HasExited) simulator.Kill(entireProcessTree: true);
                await simulator.WaitForExitAsync(ct);
                Console.WriteLine(await simulator.StandardOutput.ReadToEndAsync(ct));
                Console.WriteLine(await simulator.StandardError.ReadToEndAsync(ct));
            }
            await EventuallyAsync(async () => (await client.GetFromJsonAsync<DeviceDto[]>("/api/v1/devices", ct))!.All(d => !d.ReportedOnline), ct);
            DeviceDto beforeOldMessage = (await client.GetFromJsonAsync<DeviceDto>("/api/v1/devices/esp32-water", ct))!;
            await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<CidadeVivaDbContext>();
                Assert.True(await db.SystemEvents.CountAsync(ct) >= 3);
                await scope.ServiceProvider.GetRequiredService<IDeviceMessageProcessor>().ProcessAsync("cidade-viva/v1/esp32-water/telemetry",
                    JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = "1.0", deviceId = "esp32-water", messageId = Guid.NewGuid().ToString("N"),
                        timestamp = DateTimeOffset.UtcNow.AddMinutes(-5), origin = "SIMULATED_INPUT", quality = "VALID", mode = "MANUAL", uptimeMs = 1,
                        measurements = new { soilHumidityPct = 1.0, reservoirLow = false }, actuators = new { pumpOn = true } }), false, ct);
                // Late ACK is audited, but cannot erase the fact that the command timed out.
                await scope.ServiceProvider.GetRequiredService<IDeviceMessageProcessor>().ProcessAsync("cidade-viva/v1/esp32-water/ack",
                    JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = "1.0", deviceId = "esp32-water", commandId = unconfirmed.Id,
                        status = "APPLIED", observedState = new { pumpOn = false }, uptimeMs = 100 }), false, ct);
            }
            DeviceDto afterOldMessage = (await client.GetFromJsonAsync<DeviceDto>("/api/v1/devices/esp32-water", ct))!;
            Assert.Equal(beforeOldMessage.LastTelemetryAt, afterOldMessage.LastTelemetryAt);
            Assert.False(afterOldMessage.ReportedOnline);
            Assert.False(afterOldMessage.Actuators.GetProperty("pumpOn").GetBoolean());
            Assert.Equal("TIMED_OUT", (await client.GetFromJsonAsync<CommandDto>($"/api/v1/commands/{unconfirmed.Id}", ct))!.Status);
        }
        finally
        {
            // Only this test's freshly generated resources can be removed.
            await CleanupAsync(databaseName, brokerName, admin);
        }
    }

    private static async Task ReceiveEventAsync(WebSocket socket, string eventName, CancellationToken ct)
    {
        byte[] buffer = new byte[16384];
        string pending = "";
        for (int i = 0; i < 30; i++)
        {
            var result = await socket.ReceiveAsync(buffer, ct);
            pending += Encoding.UTF8.GetString(buffer, 0, result.Count);
            string[] messages = pending.Split('\u001e');
            foreach (string json in messages[..^1])
            {
                if (string.IsNullOrEmpty(json)) continue;
                JsonElement message = JsonSerializer.Deserialize<JsonElement>(json);
                if (message.TryGetProperty("target", out JsonElement target) && target.GetString() == eventName) return;
            }
            pending = messages[^1];
        }
        Assert.Fail("SignalR event was not observed.");
    }

    private static async Task CleanupAsync(string databaseName, string brokerName, NpgsqlConnection admin)
    {
        if (!databaseName.StartsWith("cv_backend_it_", StringComparison.Ordinal) || !brokerName.StartsWith("cv-mqtt-it-", StringComparison.Ordinal))
            throw new InvalidOperationException("Unsafe test cleanup target.");
        await DockerAsync(["rm", "-f", brokerName], CancellationToken.None, allowFailure: true);
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync(CancellationToken.None);
    }

    private static async Task PublishAsync(IMqttClient client, string kind, object payload, CancellationToken ct) =>
        await client.PublishAsync(new MqttApplicationMessageBuilder().WithTopic($"cidade-viva/v1/esp32-water/{kind}")
            .WithPayload(JsonSerializer.Serialize(payload)).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce).Build(), ct);

    private static async Task<string> LoginAsync(HttpClient client, string username, string password, CancellationToken ct)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/login", new { username, password }, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("accessToken").GetString()!;
    }

    private static async Task EventuallyAsync(Func<Task<bool>> condition, CancellationToken ct)
    {
        for (int i = 0; i < 100; i++) { if (await condition()) return; await Task.Delay(100, ct); }
        Assert.Fail("Condition not reached in 10 seconds.");
    }

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CidadeViva.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static Dictionary<string, string> ReadSimulatorConfig(string root) => File.ReadAllLines(Path.Combine(root, ".env"))
        .Where(l => l.StartsWith("MQTT_SIMULATOR_", StringComparison.Ordinal) && l.Contains('=', StringComparison.Ordinal))
        .Select(l => l.Split('=', 2)).ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.Ordinal);

    private static async Task<string> DockerAsync(string[] args, CancellationToken ct, bool allowFailure = false)
    {
        var start = new ProcessStartInfo("docker") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string arg in args) start.ArgumentList.Add(arg);
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Docker unavailable.");
        Task<string> output = process.StandardOutput.ReadToEndAsync(ct);
        Task<string> error = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        if (process.ExitCode != 0 && !allowFailure) throw new InvalidOperationException("Isolated Docker operation failed: " + await error);
        return await output;
    }
}
