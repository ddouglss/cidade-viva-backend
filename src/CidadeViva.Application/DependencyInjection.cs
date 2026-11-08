using CidadeViva.Application.Abstractions.Health;
using Microsoft.Extensions.DependencyInjection;

namespace CidadeViva.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IReadinessEvaluator, ReadinessEvaluator>();

        return services;
    }
}
