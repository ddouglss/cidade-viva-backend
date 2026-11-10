using System.Text;
using System.Text.Json;
using CidadeViva.Application.City;
using Xunit;

namespace CidadeViva.UnitTests;

public sealed class CityContractTests
{
    private const string ValidWater = """
        {"schemaVersion":"1.0","deviceId":"esp32-water","messageId":"test-1","uptimeMs":10,
        "origin":"SIMULATED_INPUT","quality":"VALID","mode":"MANUAL",
        "measurements":{"soilHumidityPct":37.2,"reservoirLow":false},"actuators":{"pumpOn":false}}
        """;

    [Fact]
    public void ValidSimulatedTelemetryWithoutTimestampIsAcceptedWithoutInventingObservationTime()
    {
        MqttEnvelope result = MqttEnvelope.Parse("cidade-viva/v1/esp32-water/telemetry", Encoding.UTF8.GetBytes(ValidWater), false);
        Assert.Equal("SIMULATED_INPUT", result.Body.GetProperty("origin").GetString());
        Assert.False(result.Body.TryGetProperty("timestamp", out _));
    }

    [Theory]
    [InlineData("esp32-energy", """{"schemaVersion":"1.0","messageId":"esp32-energy-abc-1","deviceId":"esp32-energy","uptimeMs":1000,"origin":"CALCULATED","quality":"INVALID","mode":"MANUAL","measurements":{},"actuators":{"mainLightOn":false}}""")]
    [InlineData("esp32-water", """{"schemaVersion":"1.0","messageId":"esp32-water-abc-1","deviceId":"esp32-water","uptimeMs":1000,"origin":"PHYSICAL_SENSOR","quality":"UNSYNCHRONIZED_TIME","mode":"MANUAL","measurements":{"soilAdcRaw":1775,"reservoirLow":true},"actuators":{"pumpOn":false}}""")]
    public void TelemetryOnlyFirmwarePayloadsMatchContract(string deviceKey, string payload)
    {
        MqttEnvelope envelope = MqttEnvelope.Parse($"cidade-viva/v1/{deviceKey}/telemetry", Encoding.UTF8.GetBytes(payload), false);
        Assert.Equal(deviceKey, envelope.DeviceKey);
        Assert.False(envelope.Body.TryGetProperty("timestamp", out _));
    }

    [Theory]
    [InlineData("esp32-energy")]
    [InlineData("esp32-water")]
    public void RetainedFirmwareStatusAndWillMatchContract(string deviceKey)
    {
        foreach (bool online in new[] { true, false })
        {
            string payload = $$"""{"schemaVersion":"1.0","deviceId":"{{deviceKey}}","online":{{online.ToString().ToLowerInvariant()}},"firmwareVersion":"0.3.0-telemetry-only","uptimeMs":1000,"lastKnownMode":"MANUAL"}""";
            MqttEnvelope envelope = MqttEnvelope.Parse($"cidade-viva/v1/{deviceKey}/status", Encoding.UTF8.GetBytes(payload), true);
            Assert.Equal(online, envelope.Body.GetProperty("online").GetBoolean());
        }
    }

    [Theory]
    [InlineData("1.0", "2.0")]
    [InlineData("SIMULATED_INPUT", "FAKE")]
    [InlineData("37.2", "101")]
    [InlineData("\"uptimeMs\":10", "\"uptimeMs\":\"bad\"")]
    [InlineData("\"reservoirLow\":false", "\"reservoirLow\":0")]
    public void InvalidDataIsRejected(string original, string replacement) =>
        Assert.Throws<CityRequestException>(() => MqttEnvelope.Parse("cidade-viva/v1/esp32-water/telemetry",
            Encoding.UTF8.GetBytes(ValidWater.Replace(original, replacement, StringComparison.Ordinal)), false));

    [Fact]
    public void TopicDeviceMismatchAndRetainedTelemetryAreRejected()
    {
        Assert.Throws<CityRequestException>(() => MqttEnvelope.Parse("cidade-viva/v1/esp32-energy/telemetry", Encoding.UTF8.GetBytes(ValidWater), false));
        Assert.Throws<CityRequestException>(() => MqttEnvelope.Parse("cidade-viva/v1/esp32-water/telemetry", Encoding.UTF8.GetBytes(ValidWater), true));
        Assert.Throws<CityRequestException>(() => MqttEnvelope.Parse("cidade-viva/v1/esp32-water/telemetry", new byte[65537], false));
    }

    [Theory]
    [InlineData("esp32-water", "PUMP_PULSE", "{\"durationMs\":0}")]
    [InlineData("esp32-water", "PUMP_PULSE", "{\"durationMs\":3001}")]
    [InlineData("esp32-water", "PUMP_PULSE", "{\"durationMs\":\"2500\"}")]
    [InlineData("esp32-energy", "PUMP_PULSE", "{\"durationMs\":2500}")]
    [InlineData("esp32-water", "SET_IRRIGATION_MODE", "{\"mode\":\"UNKNOWN\"}")]
    [InlineData("esp32-energy", "SET_LIGHT_MAIN", "{\"on\":1}")]
    [InlineData("esp32-energy", "REQUEST_CROSSING", "{\"extra\":true}")]
    public void UnsafeOrUnexpectedCommandsAreRejected(string key, string action, string json) =>
        Assert.Throws<CityRequestException>(() => CommandPolicy.Validate(key, action, JsonSerializer.Deserialize<JsonElement>(json)));

    [Fact]
    public void PulseWithinLaboratoryLimitIsAccepted() =>
        Assert.Equal("{\"durationMs\":2500}", CommandPolicy.Validate("esp32-water", "PUMP_PULSE", JsonSerializer.Deserialize<JsonElement>("{\"durationMs\":2500}")));
}
