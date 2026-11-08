using Microsoft.EntityFrameworkCore;
using CidadeViva.Domain.City;

namespace CidadeViva.Infrastructure.Persistence;

/// <summary>
/// DbContext base da fundação local (Entrega B).
/// Entidades de domínio e migrações (zones, devices, readings, commands, acks, alerts, rules)
/// serão adicionadas na etapa B1 conforme SDD 001 / pacote de execução.
/// </summary>
public sealed class CidadeVivaDbContext(DbContextOptions<CidadeVivaDbContext> options) : DbContext(options)
{
    public DbSet<Zone> Zones => Set<Zone>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<TelemetryMessage> Messages => Set<TelemetryMessage>();
    public DbSet<SensorReading> Readings => Set<SensorReading>();
    public DbSet<DeviceCommand> Commands => Set<DeviceCommand>();
    public DbSet<CommandAcknowledgement> Acknowledgements => Set<CommandAcknowledgement>();
    public DbSet<CityAlert> Alerts => Set<CityAlert>();
    public DbSet<LocalUser> Users => Set<LocalUser>();
    public DbSet<AutomationRule> Rules => Set<AutomationRule>();
    public DbSet<SystemEvent> SystemEvents => Set<SystemEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.Entity<Zone>(e =>
        {
            e.ToTable("zones"); e.HasKey(x => x.Id); e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(64); e.Property(x => x.Name).HasMaxLength(128);
        });
        modelBuilder.Entity<Device>(e =>
        {
            e.ToTable("devices"); e.HasKey(x => x.Id); e.HasIndex(x => x.DeviceKey).IsUnique();
            e.Property(x => x.DeviceKey).HasMaxLength(64); e.Property(x => x.FirmwareVersion).HasMaxLength(64);
            e.HasOne<Zone>().WithMany().HasForeignKey(x => x.ZoneId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.MeasurementsJson).HasColumnType("jsonb"); e.Property(x => x.ActuatorsJson).HasColumnType("jsonb");
        });
        modelBuilder.Entity<TelemetryMessage>(e =>
        {
            e.ToTable("telemetry_messages"); e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.DeviceId, x.MessageId }).IsUnique(); e.Property(x => x.MessageId).HasMaxLength(128);
            e.HasOne<Device>().WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.PayloadJson).HasColumnType("jsonb");
        });
        modelBuilder.Entity<SensorReading>(e =>
        {
            e.ToTable("sensor_readings"); e.HasKey(x => x.Id);
            e.HasOne<TelemetryMessage>().WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Device>().WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.MessageId, x.Metric }).IsUnique();
            e.HasIndex(x => new { x.DeviceId, x.ReceivedAt, x.Id }).IsDescending(false, true, true);
            e.Property(x => x.Metric).HasMaxLength(64); e.Property(x => x.ValueJson).HasColumnType("jsonb");
        });
        modelBuilder.Entity<DeviceCommand>(e =>
        {
            e.ToTable("device_commands"); e.HasKey(x => x.Id); e.HasIndex(x => new { x.Status, x.RequestedAt });
            e.HasOne<Device>().WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.ParametersJson).HasColumnType("jsonb"); e.Property(x => x.ObservedStateJson).HasColumnType("jsonb");
            e.Property(x => x.Status).IsConcurrencyToken();
        });
        modelBuilder.Entity<CommandAcknowledgement>(e =>
        {
            e.ToTable("command_acknowledgements"); e.HasKey(x => x.Id);
            e.HasOne<DeviceCommand>().WithMany().HasForeignKey(x => x.CommandId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.CommandId, x.Status }).IsUnique(); e.Property(x => x.PayloadJson).HasColumnType("jsonb");
        });
        modelBuilder.Entity<CityAlert>(e =>
        {
            e.ToTable("alerts"); e.HasKey(x => x.Id);
            e.HasOne<Device>().WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.DeviceId, x.Cause }).IsUnique().HasFilter("active = true");
        });
        modelBuilder.Entity<LocalUser>(e =>
        {
            e.ToTable("users"); e.HasKey(x => x.Id); e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.Username).HasMaxLength(128);
        });
        modelBuilder.Entity<AutomationRule>(e =>
        {
            e.ToTable("automation_rules"); e.HasKey(x => x.Id);
            e.HasOne<Device>().WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.DeviceId, x.RuleType }).IsUnique(); e.Property(x => x.Version).IsConcurrencyToken();
        });
        modelBuilder.Entity<SystemEvent>(e =>
        {
            e.ToTable("system_events"); e.HasKey(x => x.Id);
            e.HasOne<Device>().WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.PayloadJson).HasColumnType("jsonb");
            e.HasIndex(x => new { x.DeviceId, x.OccurredAt });
        });
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties())
                property.SetColumnName(string.Concat(property.Name.Select((c, i) => char.IsUpper(c) && i > 0 ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString())));
    }
}
