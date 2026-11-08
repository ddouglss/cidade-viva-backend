using Microsoft.Extensions.Primitives;

namespace CidadeViva.Api.Middleware;

public sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string correlationId = ResolveCorrelationId(context);
        context.Items[ItemKey] = correlationId;
        context.TraceIdentifier = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using IDisposable? scope = logger.BeginScope(new Dictionary<string, object>
        {
            [ItemKey] = correlationId
        });

        await next(context).ConfigureAwait(false);
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out StringValues values))
        {
            string? candidate = values.FirstOrDefault()?.Trim();
            if (!string.IsNullOrWhiteSpace(candidate) &&
                candidate.Length <= 64 &&
                candidate.All(IsAllowedCorrelationChar))
            {
                return candidate;
            }
        }

        return Guid.NewGuid().ToString("N");
    }

    private static bool IsAllowedCorrelationChar(char c) =>
        char.IsLetterOrDigit(c) || c is '-' or '_' or '.';
}
