using System.Text.Json;
using System.Text.Json.Nodes;

namespace InterviewPrep.Infrastructure;

/// <summary>Saves API keys the user types on the Settings page into a local
/// per-user config file (<c>%USERPROFILE%\.krishnaagent\appsettings.Local.json</c>),
/// in the same "AiProviders/Options" shape that <see cref="AppConfig"/> reads.
/// One key per vendor is enough — AppConfig shares it with every same-vendor
/// model. Keys never leave the machine except when calling that AI provider.</summary>
public static class KeyStore
{
    /// <summary>The base vendor ids the Settings page collects one key each for.</summary>
    public static readonly IReadOnlyList<string> Vendors =
        new[] { "groq", "gemini", "openrouter", "nvidia", "openai" };

    /// <summary>Full path of the per-user settings file the keys are written to.</summary>
    public static string ConfigPath
    {
        get
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".krishnaagent", "appsettings.Local.json");
        }
    }

    /// <summary>Merges the given vendor keys into the per-user config file. Only
    /// vendors with a non-blank value are written; blanks leave the existing key
    /// untouched. Returns the path the file was written to.</summary>
    public static string Save(IReadOnlyDictionary<string, string?> vendorKeys)
    {
        var path = ConfigPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        JsonObject root;
        try
        {
            root = File.Exists(path)
                ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? new JsonObject()
                : new JsonObject();
        }
        catch (JsonException)
        {
            // A corrupted file should not block saving; start fresh.
            root = new JsonObject();
        }

        if (root["AiProviders"] is not JsonObject providers)
        {
            providers = new JsonObject();
            root["AiProviders"] = providers;
        }

        if (providers["Options"] is not JsonArray options)
        {
            options = new JsonArray();
            providers["Options"] = options;
        }

        foreach (var vendor in Vendors)
        {
            if (!vendorKeys.TryGetValue(vendor, out var value) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var key = value.Trim();
            var existing = options.FirstOrDefault(o =>
                o is JsonObject jo &&
                string.Equals(jo["Id"]?.GetValue<string>(), vendor, StringComparison.OrdinalIgnoreCase))
                as JsonObject;

            if (existing is not null)
            {
                existing["ApiKey"] = key;
            }
            else
            {
                options.Add(new JsonObject { ["Id"] = vendor, ["ApiKey"] = key });
            }
        }

        var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
        return path;
    }
}
