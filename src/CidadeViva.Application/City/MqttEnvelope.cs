using System.Text.Json;

namespace CidadeViva.Application.City;

public sealed record MqttEnvelope(string DeviceKey, string Kind, JsonElement Body)
{
    public static MqttEnvelope Parse(string topic, ReadOnlyMemory<byte> payload, bool retained)
    {
        if (payload.Length > 65536) throw new CityRequestException(400, "PAYLOAD_TOO_LARGE");
        string[] parts = topic.Split('/');
        if (parts.Length != 4 || parts[0] != "cidade-viva" || parts[1] != "v1" || parts[2] is not ("esp32-energy" or "esp32-water"))
            throw new CityRequestException(400, "INVALID_TOPIC");
        if (parts.Length != 4 || parts[3] is not ("telemetry" or "status" or "ack")) throw new CityRequestException(400, "INVALID_TOPIC");
        JsonElement body;
        try { using JsonDocument doc = JsonDocument.Parse(payload, new JsonDocumentOptions { MaxDepth = 12 }); body = doc.RootElement.Clone(); }
        catch (JsonException) { throw new CityRequestException(400, "INVALID_JSON"); }
        if (body.ValueKind != JsonValueKind.Object || HasDuplicateKeys(body) || Text(body, "schemaVersion") != "1.0" || Text(body, "deviceId") != parts[2])
            throw new CityRequestException(400, "INCOMPATIBLE_ENVELOPE");
        if (parts[3] != "status" && retained) throw new CityRequestException(400, "RETAINED_DATA_REJECTED");
        if (!body.TryGetProperty("uptimeMs", out JsonElement uptime) || uptime.ValueKind != JsonValueKind.Number || !uptime.TryGetInt64(out long ms) || ms < 0)
            throw new CityRequestException(400, "INVALID_UPTIME");
        if (parts[3] == "telemetry")
        {
            if (Text(body, "messageId") is not { Length: > 0 and <= 128 } ||
                Text(body, "origin") is not ("PHYSICAL_SENSOR" or "SIMULATED_INPUT" or "CALCULATED") ||
                Text(body, "quality") is not ("VALID" or "INVALID" or "STALE" or "UNSYNCHRONIZED_TIME") ||
                Text(body, "mode") is not ("AUTO" or "MANUAL" or "FAULT_SENSOR") ||
                !body.TryGetProperty("measurements", out JsonElement measurements) || measurements.ValueKind != JsonValueKind.Object ||
                !body.TryGetProperty("actuators", out JsonElement actuators) || actuators.ValueKind != JsonValueKind.Object)
                throw new CityRequestException(400, "INVALID_TELEMETRY");
            if (measurements.EnumerateObject().Any(p => p.Name.Length is < 1 or > 64 ||
                p.Value.ValueKind is not (JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False) ||
                p.Value.ValueKind == JsonValueKind.Number && (!p.Value.TryGetDouble(out double n) || !double.IsFinite(n))))
                throw new CityRequestException(400, "INVALID_MEASUREMENT");
            if (measurements.TryGetProperty("soilHumidityPct", out JsonElement humidity) &&
                (humidity.ValueKind != JsonValueKind.Number || humidity.GetDouble() is < 0 or > 100))
                throw new CityRequestException(400, "INVALID_HUMIDITY");
            foreach (string boolean in new[] { "reservoirLow", "pumpOn", "mainLightOn" })
            {
                JsonElement parent = boolean == "reservoirLow" ? measurements : actuators;
                if (parent.TryGetProperty(boolean, out JsonElement value) && value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    throw new CityRequestException(400, "INVALID_BOOLEAN_STATE");
            }
        }
        if (parts[3] == "status" && (!body.TryGetProperty("online", out JsonElement online) ||
            online.ValueKind is not (JsonValueKind.True or JsonValueKind.False) || Text(body, "firmwareVersion") is not { Length: > 0 and <= 64 } ||
            Text(body, "lastKnownMode") is not { Length: > 0 and <= 32 })) throw new CityRequestException(400, "INVALID_STATUS");
        if (parts[3] == "ack" && (!Guid.TryParse(Text(body, "commandId"), out _) ||
            Text(body, "status") is not ("RECEIVED" or "APPLIED" or "REJECTED" or "EXPIRED") ||
            Text(body, "reason") is { Length: > 128 } ||
            !body.TryGetProperty("observedState", out JsonElement observed) || observed.ValueKind != JsonValueKind.Object))
            throw new CityRequestException(400, "INVALID_ACK");
        if (body.TryGetProperty("timestamp", out JsonElement timestamp) && (timestamp.ValueKind != JsonValueKind.String ||
            !timestamp.TryGetDateTimeOffset(out DateTimeOffset when) || when.Offset != TimeSpan.Zero))
            throw new CityRequestException(400, "INVALID_TIMESTAMP");
        return new MqttEnvelope(parts[2], parts[3], body);
    }

    public static string? Text(JsonElement body, string property) =>
        body.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool HasDuplicateKeys(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => value.EnumerateObject().GroupBy(p => p.Name, StringComparer.Ordinal).Any(g => g.Count() > 1) ||
                                value.EnumerateObject().Any(p => HasDuplicateKeys(p.Value)),
        JsonValueKind.Array => value.EnumerateArray().Any(HasDuplicateKeys),
        _ => false
    };
}
