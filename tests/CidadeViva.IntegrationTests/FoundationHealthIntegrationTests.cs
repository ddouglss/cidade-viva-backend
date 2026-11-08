using System.Net;
using System.Net.Http.Json;
using CidadeViva.Api.Endpoints;
using CidadeViva.Api.Middleware;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MQTTnet;
using Xunit;

namespace CidadeViva.IntegrationTests;

public sealed class FoundationHealthIntegrationTests
{
    internal static Dictionary<string, string?> LoadExternalTestConfig(
        string? customClientId = null,
        int? overrideDbPort = null,
        int? overrideMqttPort = null,
        string? overrideMqttPassword = null)
    {
        Dictionary<string, string> envFileVars = ReadDotEnvIfPresent();

        string GetVar(string key) =>
            Environment.GetEnvironmentVariable(key)
            ?? (envFileVars.TryGetValue(key, out string? v) ? v : string.Empty);

        string dbHost = GetVar("POSTGRES_HOST");
        if (string.IsNullOrWhiteSpace(dbHost))
        {
            dbHost = "127.0.0.1";
        }

        int dbPort = overrideDbPort ?? (int.TryParse(GetVar("POSTGRES_PORT"), out int pPort) ? pPort : 5432);
        string dbName = GetVar("POSTGRES_DB");
        string dbUser = GetVar("POSTGRES_USER");
        string dbPass = GetVar("POSTGRES_PASSWORD");

        string mqttHost = GetVar("MQTT_HOST");
        if (string.IsNullOrWhiteSpace(mqttHost))
        {
            mqttHost = "127.0.0.1";
        }

        int mqttPort = overrideMqttPort ?? (int.TryParse(GetVar("MQTT_PORT"), out int mPort) ? mPort : 1883);
        string mqttUser = GetVar("MQTT_BACKEND_USER");
        string mqttPass = overrideMqttPassword ?? GetVar("MQTT_BACKEND_PASSWORD");
        string clientId = customClientId ?? $"cv-it-{Guid.NewGuid():N}"[..24];

        return new Dictionary<string, string?>
        {
            ["City:Enabled"] = "false",
            ["Database:Host"] = dbHost,
            ["Database:Port"] = dbPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Database:Database"] = dbName,
            ["Database:Username"] = dbUser,
            ["Database:Password"] = dbPass,
            ["Database:CommandTimeoutSeconds"] = "2",
            ["Mqtt:Host"] = mqttHost,
            ["Mqtt:Port"] = mqttPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Mqtt:ClientId"] = clientId,
            ["Mqtt:Username"] = mqttUser,
            ["Mqtt:Password"] = mqttPass,
            ["Mqtt:ConnectTimeoutSeconds"] = "2",
            ["Mqtt:ReconnectDelaySeconds"] = "1"
        };
    }

    private static Dictionary<string, string> ReadDotEnvIfPresent()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, ".env");
            if (File.Exists(candidate))
            {
                foreach (string line in File.ReadAllLines(candidate))
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
                    {
                        continue;
                    }

                    int idx = trimmed.IndexOf('=', StringComparison.Ordinal);
                    if (idx > 0)
                    {
                        string k = trimmed[..idx].Trim();
                        string v = trimmed[(idx + 1)..].Trim();
                        result[k] = v;
                    }
                }

                break;
            }

            dir = dir.Parent;
        }

        return result;
    }

    private static WebApplicationFactory<Program> CreateFactory(IDictionary<string, string?> configOverrides)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configBuilder) =>
                {
                    configBuilder.AddInMemoryCollection(configOverrides);
                });
            });
    }

    [Fact]
    public async Task LiveAndReadyShouldReturn200WhenPostgresAndMqttAreAvailable()
    {
        Dictionary<string, string?> cfg = LoadExternalTestConfig();
        await using WebApplicationFactory<Program> factory = CreateFactory(cfg);
        using HttpClient client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, "corr-test-ready-200");

        using HttpResponseMessage liveResp = await client.GetAsync(new Uri("/api/v1/health/live", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, liveResp.StatusCode);
        Assert.True(liveResp.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out IEnumerable<string>? corrValues));
        Assert.Contains("corr-test-ready-200", corrValues);

        LivenessResponse? liveBody = await liveResp.Content.ReadFromJsonAsync<LivenessResponse>();
        Assert.NotNull(liveBody);
        Assert.Equal("Live", liveBody.Status);

        using HttpResponseMessage readyResp = await client.GetAsync(new Uri("/api/v1/health/ready", UriKind.Relative));
        string rawReady = await readyResp.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, readyResp.StatusCode);

        ReadinessResponse? readyBody = await readyResp.Content.ReadFromJsonAsync<ReadinessResponse>();
        Assert.NotNull(readyBody);
        Assert.Equal("Ready", readyBody.Status);
        Assert.Equal("Ready", readyBody.Checks.Database);
        Assert.Equal("Ready", readyBody.Checks.Mqtt);

        // Garantir que segredos nao aparecem na resposta
        string? dbSecret = cfg["Database:Password"];
        string? mqttSecret = cfg["Mqtt:Password"];
        if (!string.IsNullOrEmpty(dbSecret))
        {
            Assert.DoesNotContain(dbSecret, rawReady, StringComparison.Ordinal);
        }

        if (!string.IsNullOrEmpty(mqttSecret))
        {
            Assert.DoesNotContain(mqttSecret, rawReady, StringComparison.Ordinal);
        }

        // OpenAPI disponivel com rotas de health documentadas
        using HttpResponseMessage openApiResp = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, openApiResp.StatusCode);
        string openApiJson = await openApiResp.Content.ReadAsStringAsync();
        Assert.Contains("/api/v1/health/live", openApiJson, StringComparison.Ordinal);
        Assert.Contains("/api/v1/health/ready", openApiJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DependenciesUnavailableAndRecoveredShouldTransitionBetween503And200WithoutRestart()
    {
        Dictionary<string, string?> baseCfg = LoadExternalTestConfig();
        string realDbHost = baseCfg["Database:Host"] ?? "127.0.0.1";
        int realDbPort = int.Parse(baseCfg["Database:Port"] ?? "5432", System.Globalization.CultureInfo.InvariantCulture);
        string realMqttHost = baseCfg["Mqtt:Host"] ?? "127.0.0.1";
        int realMqttPort = int.Parse(baseCfg["Mqtt:Port"] ?? "1883", System.Globalization.CultureInfo.InvariantCulture);

        await using var dbForwarder = new TcpLoopbackForwarder(realDbHost, realDbPort);
        await using var mqttForwarder = new TcpLoopbackForwarder(realMqttHost, realMqttPort);
        dbForwarder.Start();
        mqttForwarder.Start();

        Dictionary<string, string?> cfg = LoadExternalTestConfig(
            overrideDbPort: dbForwarder.ListenPort,
            overrideMqttPort: mqttForwarder.ListenPort);

        await using WebApplicationFactory<Program> factory = CreateFactory(cfg);
        using HttpClient client = factory.CreateClient();

        // 1. Ambos disponiveis -> 200 OK
        using (HttpResponseMessage initialReady = await client.GetAsync(new Uri("/api/v1/health/ready", UriKind.Relative)))
        {
            Assert.Equal(HttpStatusCode.OK, initialReady.StatusCode);
        }

        // 2. PostgreSQL indisponivel -> Live 200 e Ready 503 (Database Unavailable, Mqtt Ready)
        await dbForwarder.StopAsync();

        using (HttpResponseMessage liveWhenDbDown = await client.GetAsync(new Uri("/api/v1/health/live", UriKind.Relative)))
        {
            Assert.Equal(HttpStatusCode.OK, liveWhenDbDown.StatusCode);
        }

        using (HttpResponseMessage readyWhenDbDown = await client.GetAsync(new Uri("/api/v1/health/ready", UriKind.Relative)))
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, readyWhenDbDown.StatusCode);
            ReadinessResponse? body = await readyWhenDbDown.Content.ReadFromJsonAsync<ReadinessResponse>();
            Assert.NotNull(body);
            Assert.Equal("Unavailable", body.Status);
            Assert.Equal("Unavailable", body.Checks.Database);
            Assert.Equal("Ready", body.Checks.Mqtt);
        }

        // 3. PostgreSQL recuperado -> Ready volta a 200 sem reiniciar a API
        dbForwarder.Start();

        using (HttpResponseMessage readyAfterDbRecovery = await WaitForReadyAsync(client))
        {
            Assert.Equal(HttpStatusCode.OK, readyAfterDbRecovery.StatusCode);
            ReadinessResponse? body = await readyAfterDbRecovery.Content.ReadFromJsonAsync<ReadinessResponse>();
            Assert.NotNull(body);
            Assert.Equal("Ready", body.Status);
            Assert.Equal("Ready", body.Checks.Database);
            Assert.Equal("Ready", body.Checks.Mqtt);
        }

        // 4. MQTT indisponivel -> Live 200 e Ready 503 (Database Ready, Mqtt Unavailable)
        await mqttForwarder.StopAsync();

        using (HttpResponseMessage liveWhenMqttDown = await client.GetAsync(new Uri("/api/v1/health/live", UriKind.Relative)))
        {
            Assert.Equal(HttpStatusCode.OK, liveWhenMqttDown.StatusCode);
        }

        using (HttpResponseMessage readyWhenMqttDown = await client.GetAsync(new Uri("/api/v1/health/ready", UriKind.Relative)))
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, readyWhenMqttDown.StatusCode);
            ReadinessResponse? body = await readyWhenMqttDown.Content.ReadFromJsonAsync<ReadinessResponse>();
            Assert.NotNull(body);
            Assert.Equal("Unavailable", body.Status);
            Assert.Equal("Ready", body.Checks.Database);
            Assert.Equal("Unavailable", body.Checks.Mqtt);
        }

        // 5. MQTT recuperado -> Ready volta a 200 automaticamente sem reiniciar a API
        mqttForwarder.Start();

        using (HttpResponseMessage readyAfterMqttRecovery = await WaitForReadyAsync(client))
        {
            Assert.Equal(HttpStatusCode.OK, readyAfterMqttRecovery.StatusCode);
            ReadinessResponse? body = await readyAfterMqttRecovery.Content.ReadFromJsonAsync<ReadinessResponse>();
            Assert.NotNull(body);
            Assert.Equal("Ready", body.Status);
            Assert.Equal("Ready", body.Checks.Database);
            Assert.Equal("Ready", body.Checks.Mqtt);
        }
    }

    [Fact]
    public async Task InvalidMqttCredentialsOrAnonymousClientShouldBeRejectedAndKeepApiUnready()
    {
        Dictionary<string, string?> baseCfg = LoadExternalTestConfig();
        string mqttHost = baseCfg["Mqtt:Host"] ?? "127.0.0.1";
        int mqttPort = int.Parse(baseCfg["Mqtt:Port"] ?? "1883", System.Globalization.CultureInfo.InvariantCulture);

        // 1. Cliente anonimo deve ser recusado pelo Mosquitto real
        var mqttFactory = new MqttClientFactory();
        using (IMqttClient anonymousClient = mqttFactory.CreateMqttClient())
        {
            MqttClientOptions anonOptions = new MqttClientOptionsBuilder()
                .WithTcpServer(mqttHost, mqttPort)
                .WithClientId($"cv-anon-{Guid.NewGuid():N}"[..20])
                .WithCleanSession()
                .WithTimeout(TimeSpan.FromSeconds(3))
                .Build();

            bool anonConnected = false;
            try
            {
                MqttClientConnectResult anonResult = await anonymousClient.ConnectAsync(anonOptions);
                anonConnected = anonResult.ResultCode == MqttClientConnectResultCode.Success && anonymousClient.IsConnected;
            }
            catch (Exception)
            {
                anonConnected = false;
            }

            Assert.False(anonConnected);
        }

        // 2. API com credencial MQTT invalida permanece Live 200 mas Ready 503
        string invalidPassword = "wrong-mqtt-password-for-test";
        Dictionary<string, string?> invalidMqttCfg = LoadExternalTestConfig(overrideMqttPassword: invalidPassword);

        await using WebApplicationFactory<Program> factory = CreateFactory(invalidMqttCfg);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage liveResp = await client.GetAsync(new Uri("/api/v1/health/live", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, liveResp.StatusCode);

        using HttpResponseMessage readyResp = await client.GetAsync(new Uri("/api/v1/health/ready", UriKind.Relative));
        string rawBody = await readyResp.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, readyResp.StatusCode);
        Assert.DoesNotContain(invalidPassword, rawBody, StringComparison.Ordinal);

        ReadinessResponse? readyBody = await readyResp.Content.ReadFromJsonAsync<ReadinessResponse>();
        Assert.NotNull(readyBody);
        Assert.Equal("Unavailable", readyBody.Status);
        Assert.Equal("Ready", readyBody.Checks.Database);
        Assert.Equal("Unavailable", readyBody.Checks.Mqtt);
    }

    private static async Task<HttpResponseMessage> WaitForReadyAsync(HttpClient client)
    {
        // Recovery is asynchronous: a stopped TCP forwarder may still be closing the previous MQTT session.
        // Keep the original host alive and require real recovery within a bounded deadline.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            HttpResponseMessage response = await client.GetAsync(new Uri("/api/v1/health/ready", UriKind.Relative), timeout.Token);
            if (response.StatusCode == HttpStatusCode.OK) return response;
            response.Dispose();
            await Task.Delay(100, timeout.Token);
        }
    }

    [Fact]
    public void MissingRequiredConfigurationShouldFailBootWithoutLeakingSecret()
    {
        Dictionary<string, string?> incompleteCfg = LoadExternalTestConfig();
        string validDbSecret = incompleteCfg["Database:Password"] ?? "top-secret-db-pass";
        incompleteCfg["Mqtt:Password"] = "";

        using WebApplicationFactory<Program> factory = CreateFactory(incompleteCfg);

        OptionsValidationException ex = Assert.Throws<OptionsValidationException>(() =>
        {
            using HttpClient _ = factory.CreateClient();
        });

        Assert.Contains("Mqtt:Password", ex.Message, StringComparison.Ordinal);
        if (!string.IsNullOrEmpty(validDbSecret))
        {
            Assert.DoesNotContain(validDbSecret, ex.Message, StringComparison.Ordinal);
        }
    }
}
