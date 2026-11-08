using System.Security.Claims;
using CidadeViva.Application.City;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.SignalR;

namespace CidadeViva.Api.City;

public sealed class CityHub : Hub;

public sealed partial class CityEventPublisher(IHubContext<CityHub> hub, ILogger<CityEventPublisher> logger) : ICityEventPublisher
{
    public async Task PublishAsync(string eventName, object data, CancellationToken ct)
    {
        try { await hub.Clients.All.SendAsync(eventName, data, ct).ConfigureAwait(false); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // State is already committed. Realtime failure cannot invalidate the transaction or trigger a physical retry.
            LogRealtimeFailed(logger, ex.GetType().Name);
        }
    }
    [LoggerMessage(4001, LogLevel.Warning, "Realtime notification failed ({ExceptionType}); clients must resync REST.")]
    private static partial void LogRealtimeFailed(ILogger logger, string exceptionType);
}

public sealed record LoginRequest(string Username, string Password);

public static class CityEndpoints
{
    public const string Scheme = "LocalBearer";
    private static string Actor(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public static void MapCityEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapPost("/api/v1/auth/login", async Task<IResult> (LoginRequest request, ICityService service, CancellationToken ct) =>
        {
            LocalIdentity? identity = await service.AuthenticateAsync(request.Username, request.Password, ct).ConfigureAwait(false);
            if (identity is null) return Results.Unauthorized();
            var principal = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, identity.Id.ToString()),
                new Claim(ClaimTypes.Name, identity.Username), new Claim(ClaimTypes.Role, identity.Role)], Scheme));
            return Results.SignIn(principal, authenticationScheme: Scheme);
        }).RequireRateLimiting("login").WithTags("Authentication");

        RouteGroupBuilder read = app.MapGroup("/api/v1").RequireAuthorization("reader");
        read.MapGet("/auth/me", (ClaimsPrincipal user) => TypedResults.Ok(new { id = Actor(user), username = user.Identity!.Name, role = user.FindFirstValue(ClaimTypes.Role) }));
        read.MapGet("/zones", async (ICityService service, CancellationToken ct) => TypedResults.Ok(await service.ZonesAsync(ct).ConfigureAwait(false)));
        read.MapGet("/devices", async (ICityService service, CancellationToken ct) => TypedResults.Ok(await service.DevicesAsync(ct).ConfigureAwait(false)));
        read.MapGet("/devices/{id}", async (string id, ICityService service, CancellationToken ct) => TypedResults.Ok(await service.DeviceAsync(id, ct).ConfigureAwait(false)));
        read.MapGet("/devices/{id}/telemetry", async (string id, ICityService service, CancellationToken ct,
            DateTimeOffset? from = null, DateTimeOffset? to = null, string? metric = null, int page = 1, int pageSize = 50) =>
            TypedResults.Ok(await service.ReadingsAsync(id, from, to, metric, page, pageSize, ct).ConfigureAwait(false)));
        read.MapGet("/commands/{id:guid}", async (Guid id, ICityService service, CancellationToken ct) => TypedResults.Ok(await service.CommandAsync(id, ct).ConfigureAwait(false)));
        read.MapGet("/alerts", async (ICityService service, CancellationToken ct) => TypedResults.Ok(await service.AlertsAsync(ct).ConfigureAwait(false)));

        RouteGroupBuilder write = app.MapGroup("/api/v1").RequireAuthorization("operator").RequireRateLimiting("write");
        write.MapPost("/devices/{id}/commands", async Task<Accepted<CommandDto>> (string id, CommandRequest request,
            ClaimsPrincipal user, ICityService service, CancellationToken ct) =>
        {
            CommandDto result = await service.CreateCommandAsync(id, request, Actor(user), ct).ConfigureAwait(false);
            return TypedResults.Accepted($"/api/v1/commands/{result.CommandId}", result);
        });
        write.MapPost("/alerts/{id:guid}/ack", async (Guid id, ClaimsPrincipal user, ICityService service, CancellationToken ct) =>
            TypedResults.Ok(await service.AcknowledgeAlertAsync(id, Actor(user), ct).ConfigureAwait(false)));
        write.MapPut("/automation-rules/{id:guid}", async (Guid id, RuleUpdate request, ClaimsPrincipal user, ICityService service, CancellationToken ct) =>
            TypedResults.Ok(await service.UpdateRuleAsync(id, request, Actor(user), ct).ConfigureAwait(false)));
        app.MapHub<CityHub>("/hubs/city").RequireAuthorization("reader");
    }
}
