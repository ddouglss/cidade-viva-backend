using System.Text.Json;

namespace CidadeViva.Application.City;

public static class CommandPolicy
{
    public static string Validate(string key, string action, JsonElement parameters)
    {
        if (parameters.ValueKind != JsonValueKind.Object || string.IsNullOrWhiteSpace(action))
            throw new CityRequestException(400, "INVALID_COMMAND");
        string[] expected = action switch
        {
            "PUMP_PULSE" when key == "esp32-water" => ["durationMs"],
            "SET_IRRIGATION_MODE" when key == "esp32-water" => ["mode"],
            "SET_LIGHT_MODE" when key == "esp32-energy" => ["mode"],
            "SET_LIGHT_MAIN" when key == "esp32-energy" => ["on"],
            "REQUEST_CROSSING" when key == "esp32-energy" => [],
            _ => throw new CityRequestException(400, "ACTION_NOT_ALLOWED")
        };
        JsonProperty[] properties = parameters.EnumerateObject().ToArray();
        if (properties.Length != expected.Length || properties.Any(p => !expected.Contains(p.Name, StringComparer.Ordinal)))
            throw new CityRequestException(400, "INVALID_PARAMETERS");
        if (action == "PUMP_PULSE" && (parameters.GetProperty("durationMs").ValueKind != JsonValueKind.Number ||
            !parameters.GetProperty("durationMs").TryGetInt32(out int duration) || duration is < 1 or > 3000))
            throw new CityRequestException(400, "PULSE_OUT_OF_RANGE");
        if (expected.Contains("mode", StringComparer.Ordinal) &&
            (parameters.GetProperty("mode").ValueKind != JsonValueKind.String || parameters.GetProperty("mode").GetString() is not ("AUTO" or "MANUAL")))
            throw new CityRequestException(400, "INVALID_MODE");
        if (action == "SET_LIGHT_MAIN" && parameters.GetProperty("on").ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new CityRequestException(400, "INVALID_LIGHT_STATE");
        return JsonSerializer.Serialize(properties.OrderBy(p => p.Name, StringComparer.Ordinal).ToDictionary(p => p.Name, p => p.Value));
    }
}
