using CidadeViva.Application.Abstractions.Health;
using CidadeViva.Infrastructure.Configuration;
using CidadeViva.Infrastructure.Messaging;
using CidadeViva.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using CidadeViva.Application.City;
using CidadeViva.Infrastructure.City;

namespace CidadeViva.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IValidateOptions<DatabaseOptions>, DatabaseOptionsValidator>();
        services
            .AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<MqttOptions>, MqttOptionsValidator>();
        services
            .AddOptions<MqttOptions>()
            .Bind(configuration.GetSection(MqttOptions.SectionName))
            .ValidateOnStart();

        services.AddDbContext<CidadeVivaDbContext>((sp, options) =>
        {
            DatabaseOptions dbOptions = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            options.UseNpgsql(dbOptions.BuildConnectionString());
        });

        services.AddSingleton<IDatabaseReadinessProbe, PostgresReadinessProbe>();

        services.AddSingleton<MqttConnectionManager>();
        services.AddSingleton<IMqttReadinessProbe>(sp => sp.GetRequiredService<MqttConnectionManager>());
        services.AddHostedService(sp => sp.GetRequiredService<MqttConnectionManager>());
        services.AddOptions<CityRuntimeOptions>().Bind(configuration.GetSection("City"))
            .Validate(x => x.TelemetryStaleTtlSeconds is >= 1 and <= 3600 && x.CommandTtlSeconds is >= 1 and <= 60 && x.AckTimeoutSeconds is >= 1 and <= 300,
                "Invalid City time limits.").ValidateOnStart();
        services.AddScoped<ICityService, CityService>();
        services.AddScoped<IDeviceMessageProcessor, DeviceMessageProcessor>();
        services.AddScoped<DatabaseInitializer>();
        services.AddSingleton<ICommandTransport>(sp => sp.GetRequiredService<MqttConnectionManager>());
        services.AddHostedService<CityDispatcher>();

        return services;
    }
}
