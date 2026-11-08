using CidadeViva.Application.Abstractions.Health;

namespace CidadeViva.Api.Endpoints;

public sealed record LivenessResponse(
    string Status,
    DateTimeOffset TimestampUtc);

public sealed record ReadinessComponentsDto(
    string Database,
    string Mqtt);

public sealed record ReadinessResponse(
    string Status,
    ReadinessComponentsDto Checks,
    DateTimeOffset TimestampUtc);

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        RouteGroupBuilder group = app.MapGroup("/api/v1/health")
            .WithTags("Health");

        group.MapGet("/live", (TimeProvider timeProvider) =>
            {
                var response = new LivenessResponse(
                    Status: "Live",
                    TimestampUtc: timeProvider.GetUtcNow());

                return Results.Ok(response);
            })
            .WithName("GetHealthLive")
            .Produces<LivenessResponse>(StatusCodes.Status200OK);

        group.MapGet("/ready", async (
                IReadinessEvaluator evaluator,
                CancellationToken cancellationToken) =>
            {
                ReadinessEvaluationResult result = await evaluator
                    .EvaluateAsync(cancellationToken)
                    .ConfigureAwait(false);

                var response = new ReadinessResponse(
                    Status: result.IsReady ? "Ready" : "Unavailable",
                    Checks: new ReadinessComponentsDto(
                        Database: result.Database.ToString(),
                        Mqtt: result.Mqtt.ToString()),
                    TimestampUtc: result.EvaluatedAtUtc);

                return result.IsReady
                    ? Results.Ok(response)
                    : Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable);
            })
            .WithName("GetHealthReady")
            .Produces<ReadinessResponse>(StatusCodes.Status200OK)
            .Produces<ReadinessResponse>(StatusCodes.Status503ServiceUnavailable);

        return app;
    }
}
