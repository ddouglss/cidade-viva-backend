using Npgsql;

namespace CidadeViva.Infrastructure.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 5432;

    public string Database { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public int CommandTimeoutSeconds { get; set; } = 5;

    public string BuildConnectionString()
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = Host,
            Port = Port,
            Database = Database,
            Username = Username,
            Password = Password,
            Timeout = Math.Clamp(CommandTimeoutSeconds, 1, 30),
            CommandTimeout = Math.Clamp(CommandTimeoutSeconds, 1, 30),
            Pooling = true
        };

        return builder.ConnectionString;
    }
}
