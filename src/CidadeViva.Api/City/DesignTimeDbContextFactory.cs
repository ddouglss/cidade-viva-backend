using CidadeViva.Infrastructure.Configuration;
using CidadeViva.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CidadeViva.Api.City;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CidadeVivaDbContext>
{
    public CidadeVivaDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        var options = new DatabaseOptions();
        configuration.GetSection("Database").Bind(options);
        // Migration scaffolding needs no live database. Updating a DB requires explicit real env configuration.
        return new CidadeVivaDbContext(new DbContextOptionsBuilder<CidadeVivaDbContext>()
            .UseNpgsql(options.BuildConnectionString()).Options);
    }
}
