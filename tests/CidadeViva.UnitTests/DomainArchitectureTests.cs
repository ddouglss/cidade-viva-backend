using System.Reflection;
using CidadeViva.Domain.Modules;
using Xunit;

namespace CidadeViva.UnitTests;

public sealed class DomainArchitectureTests
{
    [Fact]
    public void DomainAssemblyShouldNotDependOnEfCoreHttpOrMqtt()
    {
        Assembly domainAssembly = typeof(SystemModuleCatalog).Assembly;
        string[] referencedNames = domainAssembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(referencedNames, n => n.Contains("EntityFrameworkCore", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referencedNames, n => n.Contains("Npgsql", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referencedNames, n => n.Contains("MQTTnet", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referencedNames, n => n.Contains("AspNetCore", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SystemModuleCatalogShouldPreserveContractConstants()
    {
        Assert.Equal("1.0", SystemModuleCatalog.MqttSchemaVersion);
        Assert.Equal("esp32-energy", SystemModuleCatalog.EnergyDeviceId);
        Assert.Equal("esp32-water", SystemModuleCatalog.WaterDeviceId);
        Assert.Equal(6, SystemModuleCatalog.PlannedModules.Count);
    }
}
