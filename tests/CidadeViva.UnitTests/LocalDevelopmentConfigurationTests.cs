using CidadeViva.Api.City;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CidadeViva.UnitTests;

public sealed class LocalDevelopmentConfigurationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "cidade-viva-config-" + Guid.NewGuid());

    public LocalDevelopmentConfigurationTests() => Directory.CreateDirectory(root);

    [Fact]
    public void LoadsOnlyHierarchicalKeysFromRepositoryAncestor()
    {
        File.WriteAllText(Path.Combine(root, "CidadeViva.sln"), "");
        File.WriteAllText(Path.Combine(root, ".env"), "# comment\nDatabase__Host=127.0.0.1\nDatabase__Password=\"test=value\"\nUNRELATED=ignored\nMqtt__ClientId='test-client'\n");
        string nested = Path.Combine(root, "src", "Api");
        Directory.CreateDirectory(nested);
        var configuration = new ConfigurationBuilder();
        LocalDevelopmentConfiguration.AddRepositoryEnvironment(configuration, nested);
        IConfiguration result = configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Database:Host"] = "override" }).Build();
        Assert.Equal("override", result["Database:Host"]);
        Assert.Equal("test=value", result["Database:Password"]);
        Assert.Equal("test-client", result["Mqtt:ClientId"]);
        Assert.Null(result["UNRELATED"]);
    }

    [Fact]
    public void DoesNotLoadUnrelatedEnvWithoutSolutionMarker()
    {
        File.WriteAllText(Path.Combine(root, ".env"), "Database__Host=ignored");
        var configuration = new ConfigurationBuilder();
        LocalDevelopmentConfiguration.AddRepositoryEnvironment(configuration, root);
        Assert.Null(configuration.Build()["Database:Host"]);
    }

    [Fact]
    public void MissingEnvKeepsConfigurationUnset()
    {
        File.WriteAllText(Path.Combine(root, "CidadeViva.sln"), "");
        var configuration = new ConfigurationBuilder();
        LocalDevelopmentConfiguration.AddRepositoryEnvironment(configuration, root);
        Assert.Null(configuration.Build()["Database:Host"]);
    }

    public void Dispose() => Directory.Delete(root, recursive: true);
}
