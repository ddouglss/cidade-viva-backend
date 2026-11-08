using CidadeViva.Api.Endpoints;
using CidadeViva.Api.Middleware;
using CidadeViva.Application;
using CidadeViva.Infrastructure;
using CidadeViva.Api.City;
using CidadeViva.Application.City;
using CidadeViva.Infrastructure.City;
using Microsoft.AspNetCore.DataProtection;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    options.UseUtcTimestamp = true;
});

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        if (context.HttpContext.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out object? correlationId) &&
            correlationId is string id)
        {
            context.ProblemDetails.Extensions["correlationId"] = id;
        }

        // Nunca expor detalhes sensiveis ou stack traces na resposta ProblemDetails
        if (context.Exception is not null)
        {
            context.ProblemDetails.Detail = "Ocorreu um erro interno ao processar a requisicao.";
        }
    };
});

builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddSingleton<ICityEventPublisher, CityEventPublisher>();
builder.Services.AddAuthentication(CityEndpoints.Scheme).AddBearerToken(CityEndpoints.Scheme, options =>
{
    options.BearerTokenExpiration = TimeSpan.FromMinutes(30);
    options.Events.OnMessageReceived = context =>
    {
        // Browser SignalR WebSocket/SSE cannot set Authorization headers. Never accept query tokens on REST endpoints.
        if (context.Request.Path.StartsWithSegments("/hubs/city") && context.Request.Query.TryGetValue("access_token", out var token))
            context.Token = token.ToString();
        return Task.CompletedTask;
    };
});
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("reader", p => p.RequireAuthenticatedUser().RequireRole("viewer", "operator"))
    .AddPolicy("operator", p => p.RequireAuthenticatedUser().RequireRole("operator"));
builder.Services.AddDataProtection().SetApplicationName("CidadeViva.Local")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, ".keys")));
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    string[] origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:4200", "http://127.0.0.1:4200"];
    policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
    options.AddPolicy("write", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.Identity?.Name ?? "anonymous", _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) }));
});

WebApplication app = builder.Build();

if (args.Contains("--initialize-database", StringComparer.Ordinal))
{
    await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(CancellationToken.None).ConfigureAwait(false);
    return;
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.Use(async (context, next) =>
{
    try { await next(context).ConfigureAwait(false); }
    catch (CityRequestException ex)
    {
        await Results.Problem(statusCode: ex.StatusCode, title: ex.Message).ExecuteAsync(context).ConfigureAwait(false);
    }
});
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapOpenApi();
app.MapHealthEndpoints();
if (builder.Configuration.GetValue<bool>("City:Enabled")) app.MapCityEndpoints();

await app.RunAsync().ConfigureAwait(false);

public partial class Program;
