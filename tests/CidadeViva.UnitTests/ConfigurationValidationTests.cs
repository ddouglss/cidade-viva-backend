using CidadeViva.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace CidadeViva.UnitTests;

public sealed class ConfigurationValidationTests
{
    [Fact]
    public void DatabaseOptionsValidatorShouldSucceedWhenAllFieldsAreConfigured()
    {
        var validator = new DatabaseOptionsValidator();
        var options = new DatabaseOptions
        {
            Host = "127.0.0.1",
            Port = 5432,
            Database = "cidade_viva",
            Username = "cv_app",
            Password = "valid-local-test-password-123"
        };

        ValidateOptionsResult result = validator.Validate(Options.DefaultName, options);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("", 5432, "cidade_viva", "cv_app", "secret123", "Database:Host")]
    [InlineData("127.0.0.1", 0, "cidade_viva", "cv_app", "secret123", "Database:Port")]
    [InlineData("127.0.0.1", 5432, "", "cv_app", "secret123", "Database:Database")]
    [InlineData("127.0.0.1", 5432, "cidade_viva", "", "secret123", "Database:Username")]
    [InlineData("127.0.0.1", 5432, "cidade_viva", "cv_app", "", "Database:Password")]
    [InlineData("127.0.0.1", 5432, "cidade_viva", "cv_app", "CHANGE_ME_SECRET_VALUE", "Database:Password")]
    public void DatabaseOptionsValidatorShouldFailWithoutLeakingSecret(
        string host,
        int port,
        string database,
        string username,
        string password,
        string expectedProperty)
    {
        var validator = new DatabaseOptionsValidator();
        var options = new DatabaseOptions
        {
            Host = host,
            Port = port,
            Database = database,
            Username = username,
            Password = password
        };

        ValidateOptionsResult result = validator.Validate(Options.DefaultName, options);

        Assert.True(result.Failed);
        Assert.NotNull(result.FailureMessage);
        Assert.Contains(expectedProperty, result.FailureMessage, StringComparison.Ordinal);
        if (!string.IsNullOrEmpty(password))
        {
            Assert.DoesNotContain(password, result.FailureMessage, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MqttOptionsValidatorShouldSucceedWhenAllFieldsAreConfigured()
    {
        var validator = new MqttOptionsValidator();
        var options = new MqttOptions
        {
            Host = "127.0.0.1",
            Port = 1883,
            ClientId = "cidade-viva-backend-test",
            Username = "cv_backend",
            Password = "valid-mqtt-password-123"
        };

        ValidateOptionsResult result = validator.Validate(Options.DefaultName, options);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("", 1883, "client-1", "cv_backend", "mqttSecret999", "Mqtt:Host")]
    [InlineData("127.0.0.1", 70000, "client-1", "cv_backend", "mqttSecret999", "Mqtt:Port")]
    [InlineData("127.0.0.1", 1883, "", "cv_backend", "mqttSecret999", "Mqtt:ClientId")]
    [InlineData("127.0.0.1", 1883, "client-1", "", "mqttSecret999", "Mqtt:Username")]
    [InlineData("127.0.0.1", 1883, "client-1", "cv_backend", "", "Mqtt:Password")]
    [InlineData("127.0.0.1", 1883, "client-1", "cv_backend", "CHANGE_ME_MQTT_SECRET", "Mqtt:Password")]
    public void MqttOptionsValidatorShouldFailWithoutLeakingSecret(
        string host,
        int port,
        string clientId,
        string username,
        string password,
        string expectedProperty)
    {
        var validator = new MqttOptionsValidator();
        var options = new MqttOptions
        {
            Host = host,
            Port = port,
            ClientId = clientId,
            Username = username,
            Password = password
        };

        ValidateOptionsResult result = validator.Validate(Options.DefaultName, options);

        Assert.True(result.Failed);
        Assert.NotNull(result.FailureMessage);
        Assert.Contains(expectedProperty, result.FailureMessage, StringComparison.Ordinal);
        if (!string.IsNullOrEmpty(password))
        {
            Assert.DoesNotContain(password, result.FailureMessage, StringComparison.Ordinal);
        }
    }
}
