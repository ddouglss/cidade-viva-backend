namespace CidadeViva.Domain.Modules;

/// <summary>
/// Catálogo estático das fronteiras de módulos do monólito modular e constantes normativas do contrato v1.
/// O domínio permanece livre de dependências de EF Core, HTTP ou MQTT.
/// Módulos de negócio (Devices, Telemetry, Control, AutomationConfiguration, Alerts, Realtime)
/// serão implementados nas etapas autorizadas subsequentes.
/// </summary>
public static class SystemModuleCatalog
{
    public const string MqttSchemaVersion = "1.0";

    public const string EnergyDeviceId = "esp32-energy";
    public const string WaterDeviceId = "esp32-water";

    public static IReadOnlyList<string> PlannedModules { get; } =
    [
        "Devices",
        "Telemetry",
        "Control",
        "AutomationConfiguration",
        "Alerts",
        "Realtime"
    ];
}
