namespace CidadeViva.Application.Abstractions.Health;

public interface IReadinessEvaluator
{
    Task<ReadinessEvaluationResult> EvaluateAsync(CancellationToken cancellationToken = default);
}

public sealed class ReadinessEvaluator(
    IDatabaseReadinessProbe databaseProbe,
    IMqttReadinessProbe mqttProbe,
    TimeProvider timeProvider) : IReadinessEvaluator
{
    public async Task<ReadinessEvaluationResult> EvaluateAsync(CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(databaseProbe);
        ArgumentNullException.ThrowIfNull(mqttProbe);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Task<bool> dbTask = SafeCheckAsync(databaseProbe.CheckReadinessAsync, cancellationToken);
        Task<bool> mqttTask = SafeCheckAsync(mqttProbe.CheckReadinessAsync, cancellationToken);

        await Task.WhenAll(dbTask, mqttTask).ConfigureAwait(false);

        bool dbReady = await dbTask.ConfigureAwait(false);
        bool mqttReady = await mqttTask.ConfigureAwait(false);

        return new ReadinessEvaluationResult(
            IsReady: dbReady && mqttReady,
            Database: dbReady ? ComponentReadinessState.Ready : ComponentReadinessState.Unavailable,
            Mqtt: mqttReady ? ComponentReadinessState.Ready : ComponentReadinessState.Unavailable,
            EvaluatedAtUtc: timeProvider.GetUtcNow());
    }

    private static async Task<bool> SafeCheckAsync(
        Func<CancellationToken, Task<bool>> probeAction,
        CancellationToken cancellationToken)
    {
        try
        {
            return await probeAction(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
