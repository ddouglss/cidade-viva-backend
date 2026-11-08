using System.Text.Json;

namespace CidadeViva.Application.City;

public sealed record ZoneDto(Guid Id, string Code, string Name);
public sealed record DeviceDto(Guid Id, string DeviceKey, string ZoneCode, string? FirmwareVersion,
    bool Online, bool ReportedOnline, bool IsStale, string? Mode, string? Origin, string? Quality,
    DateTimeOffset? LastSeenAt, DateTimeOffset? LastTelemetryAt, JsonElement Measurements, JsonElement Actuators)
{
    public IReadOnlyList<RuleDto> AutomationRules { get; init; } = [];
}
public sealed record ReadingDto(Guid Id, string MessageId, string Metric, JsonElement Value, string? Unit,
    string Origin, string Quality, DateTimeOffset? ObservedAt, DateTimeOffset ReceivedAt);
public sealed record PageDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
public sealed record CommandRequest(string Action, JsonElement Parameters, Guid? CommandId = null);
public sealed record CommandDto(Guid CommandId, string DeviceKey, string Action, JsonElement Parameters,
    string Status, string? Reason, DateTimeOffset RequestedAt, DateTimeOffset ExpiresAt,
    DateTimeOffset? PublishedAt, string RequestedBy, JsonElement? ObservedState);
public sealed record AlertDto(Guid Id, string DeviceKey, string Cause, bool Active,
    DateTimeOffset RaisedAt, DateTimeOffset? ResolvedAt, DateTimeOffset? AcknowledgedAt, string? AcknowledgedBy);
public sealed record LocalIdentity(Guid Id, string Username, string Role);
public sealed record RuleDto(Guid Id, string DeviceKey, int MaxPulseMs, int CooldownMs, int Version,
    DateTimeOffset UpdatedAt, string UpdatedBy, string DeliveryStatus = "NOT_SENT");
public sealed record RuleUpdate(int MaxPulseMs, int CooldownMs, int ExpectedVersion);

public sealed class CityRequestException(int statusCode, string code) : Exception(code)
{
    public int StatusCode { get; } = statusCode;
}

public interface ICityService
{
    Task<IReadOnlyList<ZoneDto>> ZonesAsync(CancellationToken ct);
    Task<IReadOnlyList<DeviceDto>> DevicesAsync(CancellationToken ct);
    Task<DeviceDto> DeviceAsync(string key, CancellationToken ct);
    Task<PageDto<ReadingDto>> ReadingsAsync(string key, DateTimeOffset? from, DateTimeOffset? until,
        string? metric, int page, int pageSize, CancellationToken ct);
    Task<CommandDto> CreateCommandAsync(string key, CommandRequest request, string actor, CancellationToken ct);
    Task<CommandDto> CommandAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<AlertDto>> AlertsAsync(CancellationToken ct);
    Task<AlertDto> AcknowledgeAlertAsync(Guid id, string actor, CancellationToken ct);
    Task<RuleDto> UpdateRuleAsync(Guid id, RuleUpdate request, string actor, CancellationToken ct);
    Task<LocalIdentity?> AuthenticateAsync(string username, string password, CancellationToken ct);
}

public interface ICityEventPublisher
{
    Task PublishAsync(string eventName, object data, CancellationToken ct);
}
public interface IDeviceMessageProcessor
{
    Task ProcessAsync(string topic, ReadOnlyMemory<byte> payload, bool retained, CancellationToken ct);
}
public interface ICommandTransport
{
    Task PublishAsync(string deviceKey, string json, CancellationToken ct);
    bool IsConnected { get; }
}
