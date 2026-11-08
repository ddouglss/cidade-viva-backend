namespace CidadeViva.Domain.City;

public sealed class Zone
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class Device
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DeviceKey { get; set; } = "";
    public Guid ZoneId { get; set; }
    public string? FirmwareVersion { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }
    public bool ReportedOnline { get; set; }
    public string? Mode { get; set; }
    public string? Origin { get; set; }
    public string? Quality { get; set; }
    public string MeasurementsJson { get; set; } = "{}";
    public string ActuatorsJson { get; set; } = "{}";
    public DateTimeOffset? LastTelemetryAt { get; set; }
}

public sealed class TelemetryMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeviceId { get; set; }
    public string MessageId { get; set; } = "";
    public string Origin { get; set; } = "";
    public string Quality { get; set; } = "";
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? ObservedAt { get; set; }
    public string PayloadJson { get; set; } = "{}";
}

public sealed class SensorReading
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MessageId { get; set; }
    public Guid DeviceId { get; set; }
    public string Metric { get; set; } = "";
    public string ValueJson { get; set; } = "null";
    public string? Unit { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
}

public sealed class DeviceCommand
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeviceId { get; set; }
    public string Action { get; set; } = "";
    public string ParametersJson { get; set; } = "{}";
    public string RequestedBy { get; set; } = "";
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset? PublicationAttemptedAt { get; set; }
    public string Status { get; set; } = "PENDING";
    public string? Reason { get; set; }
    public string? ObservedStateJson { get; set; }
}

public sealed class CommandAcknowledgement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CommandId { get; set; }
    public string Status { get; set; } = "";
    public string? Reason { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public DateTimeOffset ReceivedAt { get; set; }
}

public sealed class CityAlert
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeviceId { get; set; }
    public string Cause { get; set; } = "";
    public bool Active { get; set; }
    public DateTimeOffset RaisedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public string? AcknowledgedBy { get; set; }
}

public sealed class LocalUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "viewer";
}

public sealed class AutomationRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeviceId { get; set; }
    public string RuleType { get; set; } = "IRRIGATION_LIMITS";
    public int MaxPulseMs { get; set; } = 3000;
    public int CooldownMs { get; set; } = 15000;
    public int Version { get; set; } = 1;
    public string UpdatedBy { get; set; } = "bootstrap";
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class SystemEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeviceId { get; set; }
    public string EventType { get; set; } = "";
    public string Actor { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
    public string PayloadJson { get; set; } = "{}";
}
