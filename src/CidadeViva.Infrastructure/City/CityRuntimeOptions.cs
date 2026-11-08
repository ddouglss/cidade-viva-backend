namespace CidadeViva.Infrastructure.City;

public sealed class CityRuntimeOptions
{
    public bool Enabled { get; set; }
    public int TelemetryStaleTtlSeconds { get; set; } = 30;
    public int CommandTtlSeconds { get; set; } = 10;
    public int AckTimeoutSeconds { get; set; } = 15;
}
