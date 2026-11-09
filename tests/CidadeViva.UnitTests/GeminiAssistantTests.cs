using System.Net;
using System.Text.Json;
using CidadeViva.Api.City;
using CidadeViva.Application.City;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CidadeViva.UnitTests;

public sealed class GeminiAssistantTests
{
    [Fact]
    public async Task SendsScopedContextAndReturnsServerSources()
    {
        var handler = new GeminiStub();
        using var client = new HttpClient(handler);
        using var config = new ConfigurationManager();
        config["Gemini:Enabled"] = "true";
        config["Gemini:ApiKey"] = "test-key";
        config["Gemini:Model"] = "gemini-test";
        var assistant = new GeminiAssistant(client, config, TimeProvider.System);

        AssistantAnswer answer = await assistant.AskAsync(
            new("Qual é o estado da irrigação?", "esp32-water"), new ReadOnlyCity(), CancellationToken.None);

        Assert.Equal("O reservatório baixo é simulado.", answer.Answer);
        Assert.Equal("esp32-water", Assert.Single(answer.Sources).DeviceKey);
        Assert.Equal("SIMULATED_INPUT", answer.Sources[0].Origin);
        Assert.Contains("RESERVOIR_LOW", handler.RequestBody, StringComparison.Ordinal);
        Assert.DoesNotContain("esp32-energy", handler.RequestBody, StringComparison.Ordinal);
        Assert.DoesNotContain("private-actor", handler.RequestBody, StringComparison.Ordinal);
        Assert.DoesNotContain("test-key", handler.RequestBody, StringComparison.Ordinal);
    }

    private sealed class GeminiStub : HttpMessageHandler
    {
        public string RequestBody { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("https://generativelanguage.googleapis.com/v1beta/models/gemini-test:generateContent", request.RequestUri?.AbsoluteUri);
            Assert.Equal("test-key", Assert.Single(request.Headers.GetValues("x-goog-api-key")));
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"candidates":[{"content":{"parts":[{"text":"O reservatório baixo é simulado."}]}}]}""")
            };
        }
    }

    private sealed class ReadOnlyCity : ICityService
    {
        private static DeviceDto Device(string key) => new(Guid.NewGuid(), key, "TEST", null,
            true, true, false, "AUTO", "SIMULATED_INPUT", "VALID", null, null,
            JsonSerializer.SerializeToElement(new { reservoirLow = true }), JsonSerializer.SerializeToElement(new { pumpOn = false }));

        public Task<IReadOnlyList<DeviceDto>> DevicesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<DeviceDto>>([Device("esp32-water"), Device("esp32-energy")]);

        public Task<IReadOnlyList<AlertDto>> AlertsAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AlertDto>>(
                [new(Guid.NewGuid(), "esp32-water", "RESERVOIR_LOW", true, DateTimeOffset.UtcNow, null, null, "private-actor")]);

        public Task<IReadOnlyList<ZoneDto>> ZonesAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<DeviceDto> DeviceAsync(string key, CancellationToken ct) => throw new NotSupportedException();
        public Task<PageDto<ReadingDto>> ReadingsAsync(string key, DateTimeOffset? from, DateTimeOffset? until, string? metric, int page, int pageSize, CancellationToken ct) => throw new NotSupportedException();
        public Task<CommandDto> CreateCommandAsync(string key, CommandRequest request, string actor, CancellationToken ct) => throw new NotSupportedException();
        public Task<CommandDto> CommandAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<AlertDto> AcknowledgeAlertAsync(Guid id, string actor, CancellationToken ct) => throw new NotSupportedException();
        public Task<RuleDto> UpdateRuleAsync(Guid id, RuleUpdate request, string actor, CancellationToken ct) => throw new NotSupportedException();
        public Task<LocalIdentity?> AuthenticateAsync(string username, string password, CancellationToken ct) => throw new NotSupportedException();
    }
}
