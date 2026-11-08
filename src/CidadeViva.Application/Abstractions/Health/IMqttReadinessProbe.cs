namespace CidadeViva.Application.Abstractions.Health;

public interface IMqttReadinessProbe
{
    Task<bool> CheckReadinessAsync(CancellationToken cancellationToken = default);
}
