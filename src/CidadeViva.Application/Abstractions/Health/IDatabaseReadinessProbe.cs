namespace CidadeViva.Application.Abstractions.Health;

public interface IDatabaseReadinessProbe
{
    Task<bool> CheckReadinessAsync(CancellationToken cancellationToken = default);
}
