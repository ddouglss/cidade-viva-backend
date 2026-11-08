namespace CidadeViva.Infrastructure.Configuration;

public sealed class MqttOptions
{
    public const string SectionName = "Mqtt";

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 1883;

    public string ClientId { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public int ConnectTimeoutSeconds { get; set; } = 3;

    public int ReconnectDelaySeconds { get; set; } = 2;
}
