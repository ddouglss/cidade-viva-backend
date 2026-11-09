using System.Text.Json;
using CidadeViva.Application.City;
using CidadeViva.Domain.City;
using CidadeViva.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CidadeViva.Infrastructure.City;

public sealed partial class CityDispatcher(IServiceScopeFactory scopes, IOptions<CityRuntimeOptions> options,
    TimeProvider clock, ICommandTransport transport, ILogger<CityDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1), clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                try { await TickAsync(stoppingToken).ConfigureAwait(false); }
                catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
                {
                    LogCycleFailed(logger, ex.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Stopping the host cancels both the timer wait and any active cycle.
        }
    }

    [LoggerMessage(3001, LogLevel.Warning, "Dispatcher cycle failed ({ExceptionType}).")]
    private static partial void LogCycleFailed(ILogger logger, string exceptionType);

    public async Task TickAsync(CancellationToken ct)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        CidadeVivaDbContext db = scope.ServiceProvider.GetRequiredService<CidadeVivaDbContext>();
        ICityEventPublisher events = scope.ServiceProvider.GetRequiredService<ICityEventPublisher>();
        DateTimeOffset now = clock.GetUtcNow();
        // Terminal state updates use compare-and-set; do not overwrite an ACK committed concurrently.
        var overdue = await db.Commands.AsNoTracking().Where(c =>
            c.Status == "PENDING" && c.ExpiresAt <= now ||
            (c.Status == "PUBLISHED" || c.Status == "RECEIVED") && c.PublishedAt <= now.AddSeconds(-options.Value.AckTimeoutSeconds)).ToListAsync(ct).ConfigureAwait(false);
        foreach (DeviceCommand command in overdue)
        {
            string status = command.Status == "PENDING" && command.PublicationAttemptedAt is null ? "EXPIRED" : "TIMED_OUT";
            int changed = await db.Commands.Where(c => c.Id == command.Id && c.Status == command.Status)
                .ExecuteUpdateAsync(set => set.SetProperty(c => c.Status, status).SetProperty(c => c.Reason, status), ct).ConfigureAwait(false);
            if (changed > 0) await events.PublishAsync("CommandStatusChanged",
                await scope.ServiceProvider.GetRequiredService<ICityService>().CommandAsync(command.Id, ct).ConfigureAwait(false), ct).ConfigureAwait(false);
        }
        await RefreshOfflineAlertsAsync(db, scope.ServiceProvider, now, ct).ConfigureAwait(false);
        if (!transport.IsConnected) return;
        DeviceCommand? pending;
        string deviceKey;
        await using (var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false))
        {
            pending = (await db.Commands.FromSqlRaw("SELECT * FROM device_commands WHERE status = 'PENDING' AND publication_attempted_at IS NULL ORDER BY requested_at LIMIT 1 FOR UPDATE SKIP LOCKED")
                .ToListAsync(ct).ConfigureAwait(false)).FirstOrDefault();
            if (pending is null || pending.ExpiresAt <= now) return;
            deviceKey = await db.Devices.Where(d => d.Id == pending.DeviceId).Select(d => d.DeviceKey).SingleAsync(ct).ConfigureAwait(false);
            pending.PublicationAttemptedAt = now; pending.Reason = "DISPATCH_RESERVED";
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
        }
        // Reservation is durable before touching transport. Ambiguous sends are never automatically retried.
        string json = JsonSerializer.Serialize(new { schemaVersion = "1.0", commandId = pending.Id,
            deviceId = deviceKey, action = pending.Action, parameters = JsonSerializer.Deserialize<JsonElement>(pending.ParametersJson),
            requestedAt = pending.RequestedAt, expiresAt = pending.ExpiresAt, requestedBy = pending.RequestedBy });
        string? reason = null;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            await transport.PublishAsync(deviceKey, json, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested) { reason = "PUBLICATION_UNCERTAIN"; }
        if (reason is null)
            await db.Commands.Where(c => c.Id == pending.Id && c.Status == "PENDING")
                .ExecuteUpdateAsync(set => set.SetProperty(c => c.Status, "PUBLISHED").SetProperty(c => c.PublishedAt, now).SetProperty(c => c.Reason, (string?)null), ct).ConfigureAwait(false);
        else
            await db.Commands.Where(c => c.Id == pending.Id && c.Status == "PENDING")
                .ExecuteUpdateAsync(set => set.SetProperty(c => c.Reason, reason), ct).ConfigureAwait(false);
        await events.PublishAsync("CommandStatusChanged",
            await scope.ServiceProvider.GetRequiredService<ICityService>().CommandAsync(pending.Id, ct).ConfigureAwait(false), ct).ConfigureAwait(false);
    }

    private async Task RefreshOfflineAlertsAsync(CidadeVivaDbContext db, IServiceProvider services, DateTimeOffset now, CancellationToken ct)
    {
        Guid[] ids = await db.Devices.AsNoTracking().Select(d => d.Id).ToArrayAsync(ct).ConfigureAwait(false);
        foreach (Guid id in ids)
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            string key = await db.Devices.Where(d => d.Id == id).Select(d => d.DeviceKey).SingleAsync(ct).ConfigureAwait(false);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({key}))", ct).ConfigureAwait(false);
            Device device = await db.Devices.SingleAsync(d => d.Id == id, ct).ConfigureAwait(false);
            bool stale = !device.ReportedOnline || device.LastSeenAt is null || now - device.LastSeenAt.Value > TimeSpan.FromSeconds(options.Value.TelemetryStaleTtlSeconds);
            List<(string Name, object Data)> notifications = [];
            await ((DeviceMessageProcessor)services.GetRequiredService<IDeviceMessageProcessor>())
                .SetAlertAsync(device, "DEVICE_OFFLINE", stale, now, notifications, ct).ConfigureAwait(false);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
            foreach ((string name, object data) in notifications)
                await services.GetRequiredService<ICityEventPublisher>().PublishAsync(name, data, ct).ConfigureAwait(false);
        }
    }
}
