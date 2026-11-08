using CidadeViva.Application.Abstractions.Health;
using CidadeViva.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using CidadeViva.Infrastructure.City;

namespace CidadeViva.Infrastructure.Persistence;

public sealed partial class PostgresReadinessProbe(
    IOptions<DatabaseOptions> options,
    ILogger<PostgresReadinessProbe> logger,
    IOptions<CityRuntimeOptions> cityOptions) : IDatabaseReadinessProbe
{
    public async Task<bool> CheckReadinessAsync(CancellationToken cancellationToken = default)
    {
        DatabaseOptions dbOptions = options.Value;
        int timeoutSeconds = Math.Clamp(dbOptions.CommandTimeoutSeconds, 1, 15);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            await using var connection = new NpgsqlConnection(dbOptions.BuildConnectionString());
            await connection.OpenAsync(timeoutCts.Token).ConfigureAwait(false);

            string sql = cityOptions.Value.Enabled
                ? "SELECT CASE WHEN to_regclass('public.telemetry_messages') IS NOT NULL AND to_regclass('public.system_events') IS NOT NULL THEN 1 ELSE 0 END;"
                : "SELECT 1;";
            await using var command = new NpgsqlCommand(sql, connection)
            {
                CommandTimeout = timeoutSeconds
            };

            object? scalar = await command.ExecuteScalarAsync(timeoutCts.Token).ConfigureAwait(false);
            return scalar is 1 or 1L;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                LogDatabaseReadinessFailed(logger, ex.GetType().Name);
            }

            return false;
        }
    }

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "Verificacao de readiness do PostgreSQL falhou ({ExceptionType}).")]
    private static partial void LogDatabaseReadinessFailed(ILogger logger, string exceptionType);
}
