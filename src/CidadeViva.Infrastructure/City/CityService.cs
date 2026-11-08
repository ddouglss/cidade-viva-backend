using System.Text.Json;
using CidadeViva.Application.City;
using CidadeViva.Domain.City;
using CidadeViva.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CidadeViva.Infrastructure.City;

public sealed class CityService(CidadeVivaDbContext db, TimeProvider clock, IOptions<CityRuntimeOptions> options,
    ICityEventPublisher events) : ICityService
{
    private static JsonElement Json(string value) => JsonSerializer.Deserialize<JsonElement>(value);

    public async Task<IReadOnlyList<ZoneDto>> ZonesAsync(CancellationToken ct) =>
        await db.Zones.AsNoTracking().OrderBy(z => z.Code).Select(z => new ZoneDto(z.Id, z.Code, z.Name)).ToListAsync(ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<DeviceDto>> DevicesAsync(CancellationToken ct)
    {
        var rows = await (from d in db.Devices.AsNoTracking() join z in db.Zones on d.ZoneId equals z.Id
                          orderby d.DeviceKey select new { Device = d, ZoneCode = z.Code }).ToListAsync(ct).ConfigureAwait(false);
        var rules = await db.Rules.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);
        return rows.Select(r => ToDevice(r.Device, r.ZoneCode) with { AutomationRules = rules.Where(a => a.DeviceId == r.Device.Id)
            .Select(a => new RuleDto(a.Id, r.Device.DeviceKey, a.MaxPulseMs, a.CooldownMs, a.Version, a.UpdatedAt, a.UpdatedBy)).ToArray() }).ToArray();
    }

    public async Task<DeviceDto> DeviceAsync(string key, CancellationToken ct) =>
        (await DevicesAsync(ct).ConfigureAwait(false)).FirstOrDefault(d => d.DeviceKey == key)
        ?? throw new CityRequestException(404, "DEVICE_NOT_FOUND");

    private DeviceDto ToDevice(Device d, string zoneCode)
    {
        bool stale = d.Quality == "STALE" || d.LastTelemetryAt is null || clock.GetUtcNow() - d.LastTelemetryAt.Value > TimeSpan.FromSeconds(options.Value.TelemetryStaleTtlSeconds);
        return new DeviceDto(d.Id, d.DeviceKey, zoneCode, d.FirmwareVersion, d.ReportedOnline && !stale,
            d.ReportedOnline, stale, d.Mode, d.Origin, d.Quality, d.LastSeenAt, d.LastTelemetryAt, Json(d.MeasurementsJson), Json(d.ActuatorsJson));
    }

    public async Task<PageDto<ReadingDto>> ReadingsAsync(string key, DateTimeOffset? from, DateTimeOffset? until,
        string? metric, int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || page > 1000000 || pageSize is < 1 or > 200 || from >= until ||
            from is { Offset: var fo } && fo != TimeSpan.Zero || until is { Offset: var t } && t != TimeSpan.Zero)
            throw new CityRequestException(400, "INVALID_PAGINATION_OR_RANGE");
        DeviceDto device = await DeviceAsync(key, ct).ConfigureAwait(false);
        var query = from r in db.Readings.AsNoTracking() join m in db.Messages on r.MessageId equals m.Id
                    where r.DeviceId == device.Id select new { Reading = r, Message = m };
        if (from.HasValue) query = query.Where(x => x.Reading.ReceivedAt >= from.Value);
        if (until.HasValue) query = query.Where(x => x.Reading.ReceivedAt < until.Value);
        if (metric is not null) query = query.Where(x => x.Reading.Metric == metric);
        int count = await query.CountAsync(ct).ConfigureAwait(false);
        var rows = await query.OrderByDescending(x => x.Reading.ReceivedAt).ThenByDescending(x => x.Reading.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct).ConfigureAwait(false);
        return new PageDto<ReadingDto>(rows.Select(x => new ReadingDto(x.Reading.Id, x.Message.MessageId,
            x.Reading.Metric, Json(x.Reading.ValueJson), x.Reading.Unit, x.Message.Origin, x.Message.Quality,
            x.Message.ObservedAt, x.Reading.ReceivedAt)).ToArray(), page, pageSize, count);
    }

    public async Task<CommandDto> CreateCommandAsync(string key, CommandRequest request, string actor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        DeviceDto device = await DeviceAsync(key, ct).ConfigureAwait(false);
        string parameters = CommandPolicy.Validate(key, request.Action, request.Parameters);
        if (request.CommandId == Guid.Empty) throw new CityRequestException(400, "INVALID_COMMAND_ID");
        Guid id = request.CommandId ?? Guid.NewGuid();
        DeviceCommand? existing = await db.Commands.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);
        if (existing is not null) return MatchRetry(existing, device, request.Action, parameters, actor);
        if (request.Action == "PUMP_PULSE")
        {
            int? maxPulse = await db.Rules.Where(r => r.DeviceId == device.Id && r.RuleType == "IRRIGATION_LIMITS")
                .Select(r => (int?)r.MaxPulseMs).SingleOrDefaultAsync(ct).ConfigureAwait(false);
            if (request.Parameters.GetProperty("durationMs").GetInt32() > (maxPulse ?? 3000))
                throw new CityRequestException(400, "CONFIGURED_PULSE_LIMIT_EXCEEDED");
        }
        var command = new DeviceCommand { Id = id, DeviceId = device.Id, Action = request.Action,
            ParametersJson = parameters, RequestedBy = actor, RequestedAt = clock.GetUtcNow(),
            ExpiresAt = clock.GetUtcNow().AddSeconds(options.Value.CommandTtlSeconds) };
        db.Commands.Add(command);
        try { await db.SaveChangesAsync(ct).ConfigureAwait(false); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            existing = await db.Commands.AsNoTracking().SingleAsync(x => x.Id == id, ct).ConfigureAwait(false);
            return MatchRetry(existing, device, request.Action, parameters, actor);
        }
        CommandDto result = ToCommand(command, key);
        await events.PublishAsync("CommandStatusChanged", result, ct).ConfigureAwait(false);
        return result;
    }

    private static CommandDto MatchRetry(DeviceCommand command, DeviceDto device, string action, string parameters, string actor)
    {
        if (command.DeviceId != device.Id || command.Action != action || command.RequestedBy != actor ||
            !JsonElement.DeepEquals(Json(command.ParametersJson), Json(parameters)))
            throw new CityRequestException(409, "COMMAND_ID_CONFLICT");
        return ToCommand(command, device.DeviceKey);
    }

    internal static CommandDto ToCommand(DeviceCommand c, string key) => new(c.Id, key, c.Action,
        Json(c.ParametersJson), c.Status, c.Reason, c.RequestedAt, c.ExpiresAt, c.PublishedAt, c.RequestedBy,
        c.ObservedStateJson is null ? null : Json(c.ObservedStateJson));

    public async Task<CommandDto> CommandAsync(Guid id, CancellationToken ct)
    {
        var row = await (from c in db.Commands.AsNoTracking() join d in db.Devices on c.DeviceId equals d.Id
                         where c.Id == id select new { Command = c, d.DeviceKey }).SingleOrDefaultAsync(ct).ConfigureAwait(false)
            ?? throw new CityRequestException(404, "COMMAND_NOT_FOUND");
        return ToCommand(row.Command, row.DeviceKey);
    }

    public async Task<IReadOnlyList<AlertDto>> AlertsAsync(CancellationToken ct)
    {
        var rows = await (from a in db.Alerts.AsNoTracking() join d in db.Devices on a.DeviceId equals d.Id
                          orderby a.RaisedAt descending select new { Alert = a, d.DeviceKey }).Take(200).ToListAsync(ct).ConfigureAwait(false);
        return rows.Select(x => ToAlert(x.Alert, x.DeviceKey)).ToArray();
    }

    internal static AlertDto ToAlert(CityAlert a, string key) => new(a.Id, key, a.Cause, a.Active, a.RaisedAt,
        a.ResolvedAt, a.AcknowledgedAt, a.AcknowledgedBy);

    public async Task<AlertDto> AcknowledgeAlertAsync(Guid id, string actor, CancellationToken ct)
    {
        CityAlert alert = await db.Alerts.FindAsync([id], ct).ConfigureAwait(false)
            ?? throw new CityRequestException(404, "ALERT_NOT_FOUND");
        if (alert.AcknowledgedAt is null)
        {
            alert.AcknowledgedAt = clock.GetUtcNow(); alert.AcknowledgedBy = actor;
            db.SystemEvents.Add(new SystemEvent { DeviceId = alert.DeviceId, EventType = "ALERT_ACKNOWLEDGED", Actor = actor,
                OccurredAt = clock.GetUtcNow(), PayloadJson = JsonSerializer.Serialize(new { alertId = alert.Id, causeStillActive = alert.Active }) });
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        string key = await db.Devices.Where(x => x.Id == alert.DeviceId).Select(x => x.DeviceKey).SingleAsync(ct).ConfigureAwait(false);
        return ToAlert(alert, key);
    }

    public async Task<RuleDto> UpdateRuleAsync(Guid id, RuleUpdate request, string actor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.MaxPulseMs is < 1 or > 3000 || request.CooldownMs < 15000)
            throw new CityRequestException(400, "LABORATORY_LIMITS_EXCEEDED");
        AutomationRule rule = await db.Rules.FindAsync([id], ct).ConfigureAwait(false)
            ?? throw new CityRequestException(404, "RULE_NOT_FOUND");
        if (rule.Version != request.ExpectedVersion) throw new CityRequestException(409, "RULE_VERSION_CONFLICT");
        rule.MaxPulseMs = request.MaxPulseMs; rule.CooldownMs = request.CooldownMs; rule.Version++;
        rule.UpdatedAt = clock.GetUtcNow(); rule.UpdatedBy = actor;
        db.SystemEvents.Add(new SystemEvent { DeviceId = rule.DeviceId, EventType = "AUTOMATION_RULE_UPDATED", Actor = actor,
            OccurredAt = rule.UpdatedAt, PayloadJson = JsonSerializer.Serialize(new { ruleId = rule.Id, rule.Version,
                rule.MaxPulseMs, rule.CooldownMs, deliveryStatus = "NOT_SENT" }) });
        try { await db.SaveChangesAsync(ct).ConfigureAwait(false); }
        catch (DbUpdateConcurrencyException) { throw new CityRequestException(409, "RULE_VERSION_CONFLICT"); }
        string key = await db.Devices.Where(x => x.Id == rule.DeviceId).Select(x => x.DeviceKey).SingleAsync(ct).ConfigureAwait(false);
        return new RuleDto(rule.Id, key, rule.MaxPulseMs, rule.CooldownMs, rule.Version, rule.UpdatedAt, rule.UpdatedBy);
    }

    public async Task<LocalIdentity?> AuthenticateAsync(string username, string password, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password) || username.Length > 128 || password.Length > 1024) return null;
        LocalUser? user = await db.Users.SingleOrDefaultAsync(x => x.Username == username, ct).ConfigureAwait(false);
        var hasher = new PasswordHasher<LocalUser>();
        // Always perform password hashing work even for unknown users.
        string hash = user?.PasswordHash ?? DummyPasswordHash.Value;
        var candidate = user ?? new LocalUser();
        PasswordVerificationResult verified = hasher.VerifyHashedPassword(candidate, hash, password);
        if (user is null || verified == PasswordVerificationResult.Failed) return null;
        if (verified == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, password);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        return new LocalIdentity(user.Id, user.Username, user.Role);
    }

    private static readonly Lazy<string> DummyPasswordHash = new(() =>
        new PasswordHasher<LocalUser>().HashPassword(new LocalUser(), Guid.NewGuid().ToString("N")));
}
