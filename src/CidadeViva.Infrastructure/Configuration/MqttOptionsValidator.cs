using Microsoft.Extensions.Options;

namespace CidadeViva.Infrastructure.Configuration;

public sealed class MqttOptionsValidator : IValidateOptions<MqttOptions>
{
    public ValidateOptionsResult Validate(string? name, MqttOptions options)
    {
        if (options is null)
        {
            return ValidateOptionsResult.Fail("Secao de configuracao 'Mqtt' ausente.");
        }

        var failures = new List<string>();

        if (IsMissingOrPlaceholder(options.Host))
        {
            failures.Add("Configuracao obrigatoria 'Mqtt:Host' ausente ou invalida.");
        }

        if (options.Port is < 1 or > 65535)
        {
            failures.Add("Configuracao obrigatoria 'Mqtt:Port' deve estar entre 1 e 65535.");
        }

        if (IsMissingOrPlaceholder(options.ClientId))
        {
            failures.Add("Configuracao obrigatoria 'Mqtt:ClientId' ausente ou invalida.");
        }

        if (IsMissingOrPlaceholder(options.Username))
        {
            failures.Add("Configuracao obrigatoria 'Mqtt:Username' ausente ou invalida.");
        }

        if (IsMissingOrPlaceholder(options.Password))
        {
            failures.Add("Configuracao obrigatoria 'Mqtt:Password' ausente ou nao configurada.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private static bool IsMissingOrPlaceholder(string? value) =>
        string.IsNullOrWhiteSpace(value) ||
        value.Trim().StartsWith("CHANGE_ME", StringComparison.OrdinalIgnoreCase);
}
