namespace CidadeViva.Application.Abstractions.Health;

public enum ComponentReadinessState
{
    Ready = 0,
    Unavailable = 1
}

public sealed record ReadinessEvaluationResult(
    bool IsReady,
    ComponentReadinessState Database,
    ComponentReadinessState Mqtt,
    DateTimeOffset EvaluatedAtUtc);
