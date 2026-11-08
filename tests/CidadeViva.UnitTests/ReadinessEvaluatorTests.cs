using CidadeViva.Application.Abstractions.Health;
using Xunit;

namespace CidadeViva.UnitTests;

public sealed class ReadinessEvaluatorTests
{
    [Fact]
    public async Task EvaluateAsyncShouldReturnReadyWhenBothProbesReturnTrue()
    {
        var dbProbe = new StubDatabaseProbe(true);
        var mqttProbe = new StubMqttProbe(true);
        var evaluator = new ReadinessEvaluator(dbProbe, mqttProbe, TimeProvider.System);

        ReadinessEvaluationResult result = await evaluator.EvaluateAsync();

        Assert.True(result.IsReady);
        Assert.Equal(ComponentReadinessState.Ready, result.Database);
        Assert.Equal(ComponentReadinessState.Ready, result.Mqtt);
    }

    [Theory]
    [InlineData(false, true, ComponentReadinessState.Unavailable, ComponentReadinessState.Ready)]
    [InlineData(true, false, ComponentReadinessState.Ready, ComponentReadinessState.Unavailable)]
    [InlineData(false, false, ComponentReadinessState.Unavailable, ComponentReadinessState.Unavailable)]
    public async Task EvaluateAsyncShouldReturnUnavailableWhenAnyProbeFails(
        bool dbReady,
        bool mqttReady,
        ComponentReadinessState expectedDb,
        ComponentReadinessState expectedMqtt)
    {
        var dbProbe = new StubDatabaseProbe(dbReady);
        var mqttProbe = new StubMqttProbe(mqttReady);
        var evaluator = new ReadinessEvaluator(dbProbe, mqttProbe, TimeProvider.System);

        ReadinessEvaluationResult result = await evaluator.EvaluateAsync();

        Assert.False(result.IsReady);
        Assert.Equal(expectedDb, result.Database);
        Assert.Equal(expectedMqtt, result.Mqtt);
    }

    [Fact]
    public async Task EvaluateAsyncShouldConvertProbeExceptionToUnavailable()
    {
        var dbProbe = new ThrowingDatabaseProbe();
        var mqttProbe = new StubMqttProbe(true);
        var evaluator = new ReadinessEvaluator(dbProbe, mqttProbe, TimeProvider.System);

        ReadinessEvaluationResult result = await evaluator.EvaluateAsync();

        Assert.False(result.IsReady);
        Assert.Equal(ComponentReadinessState.Unavailable, result.Database);
        Assert.Equal(ComponentReadinessState.Ready, result.Mqtt);
    }

    private sealed class StubDatabaseProbe(bool result) : IDatabaseReadinessProbe
    {
        public Task<bool> CheckReadinessAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }

    private sealed class StubMqttProbe(bool result) : IMqttReadinessProbe
    {
        public Task<bool> CheckReadinessAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }

    private sealed class ThrowingDatabaseProbe : IDatabaseReadinessProbe
    {
        public Task<bool> CheckReadinessAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated probe failure");
    }
}
