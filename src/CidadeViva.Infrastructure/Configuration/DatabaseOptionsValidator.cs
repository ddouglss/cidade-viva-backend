using Microsoft.Extensions.Options;

namespace CidadeViva.Infrastructure.Configuration;

public sealed class DatabaseOptionsValidator : IValidateOptions<DatabaseOptions>
{
    public ValidateOptionsResult Validate(string? name, DatabaseOptions options)
    {
        if (options is null)
        {
            return ValidateOptionsResult.Fail("Secao de configuracao 'Database' ausente.");
        }

        var failures = new List<string>();

        if (IsMissingOrPlaceholder(options.Host))
        {
            failures.Add("Configuracao obrigatoria 'Database:Host' ausente ou invalida.");
        }

        if (options.Port is < 1 or > 65535)
        {
            failures.Add("Configuracao obrigatoria 'Database:Port' deve estar entre 1 e 65535.");
        }

        if (IsMissingOrPlaceholder(options.Database))
        {
            failures.Add("Configuracao obrigatoria 'Database:Database' ausente ou invalida.");
        }

        if (IsMissingOrPlaceholder(options.Username))
        {
            failures.Add("Configuracao obrigatoria 'Database:Username' ausente ou invalida.");
        }

        if (IsMissingOrPlaceholder(options.Password))
        {
            failures.Add("Configuracao obrigatoria 'Database:Password' ausente ou nao configurada.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private static bool IsMissingOrPlaceholder(string? value) =>
        string.IsNullOrWhiteSpace(value) ||
        value.Trim().StartsWith("CHANGE_ME", StringComparison.OrdinalIgnoreCase);
}
