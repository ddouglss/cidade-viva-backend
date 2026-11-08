using CidadeViva.Domain.City;
using CidadeViva.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CidadeViva.Infrastructure.City;

public sealed class DatabaseInitializer(CidadeVivaDbContext db, IConfiguration configuration, TimeProvider clock)
{
    public async Task InitializeAsync(CancellationToken ct)
    {
        await db.Database.MigrateAsync(ct).ConfigureAwait(false);
        await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        // Serialize bootstrap across processes without relying on check-then-insert races.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(81083201)", ct).ConfigureAwait(false);
        foreach ((string code, string name, string key) in new[] {
            ("ENERGY_MOBILITY", "Energia e Mobilidade", "esp32-energy"),
            ("WATER_IRRIGATION", "Hidráulica e Irrigação", "esp32-water") })
        {
            Zone? zone = await db.Zones.SingleOrDefaultAsync(x => x.Code == code, ct).ConfigureAwait(false);
            if (zone is null) { zone = new Zone { Code = code, Name = name }; db.Zones.Add(zone); }
            if (!await db.Devices.AnyAsync(x => x.DeviceKey == key, ct).ConfigureAwait(false))
                db.Devices.Add(new Device { DeviceKey = key, ZoneId = zone.Id });
        }
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        Device water = await db.Devices.SingleAsync(d => d.DeviceKey == "esp32-water", ct).ConfigureAwait(false);
        if (!await db.Rules.AnyAsync(r => r.DeviceId == water.Id, ct).ConfigureAwait(false))
            db.Rules.Add(new AutomationRule { DeviceId = water.Id, UpdatedAt = clock.GetUtcNow() });
        foreach (string role in new[] { "viewer", "operator" })
        {
            string? password = configuration[$"Bootstrap:{role}:Password"];
            string? username = configuration[$"Bootstrap:{role}:Username"];
            if (password is null && username is null) continue;
            if (string.IsNullOrWhiteSpace(username) || username.Length > 128 || password is null || password.Length < 12 || password.StartsWith("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Configure Bootstrap:{role}:Username e Password (mínimo 12 caracteres), sem placeholders.");
            if (await db.Users.AnyAsync(x => x.Username == username, ct).ConfigureAwait(false)) continue;
            var user = new LocalUser { Username = username, Role = role };
            user.PasswordHash = new PasswordHasher<LocalUser>().HashPassword(user, password);
            db.Users.Add(user);
        }
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }
}
