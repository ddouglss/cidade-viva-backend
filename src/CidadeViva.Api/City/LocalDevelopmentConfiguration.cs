namespace CidadeViva.Api.City;

public static class LocalDevelopmentConfiguration
{
    public static void AddRepositoryEnvironment(IConfigurationBuilder configuration, params string[] searchRoots)
    {
        foreach (string root in searchRoots)
        {
            for (DirectoryInfo? directory = new(root); directory is not null; directory = directory.Parent)
            {
                if (!File.Exists(Path.Combine(directory.FullName, "CidadeViva.sln"))) continue;
                string path = Path.Combine(directory.FullName, ".env");
                if (!File.Exists(path)) break;

                var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                foreach (string line in File.ReadLines(path))
                {
                    string trimmed = line.Trim();
                    int separator = trimmed.IndexOf('=', StringComparison.Ordinal);
                    if (trimmed.StartsWith('#') || separator <= 0) continue;
                    string key = trimmed[..separator].Trim();
                    if (!key.Contains("__", StringComparison.Ordinal)) continue;
                    string value = trimmed[(separator + 1)..].Trim();
                    if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
                        value = value[1..^1];
                    values[key.Replace("__", ":", StringComparison.Ordinal)] = value;
                }
                configuration.AddInMemoryCollection(values);
                return;
            }
        }
    }
}
