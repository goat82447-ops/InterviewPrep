using System.Text.Json;

namespace InterviewPrep.Infrastructure;

/// <summary>One selectable AI model/provider that speaks the OpenAI-compatible
/// chat-completions protocol (works for OpenAI/ChatGPT, Groq, and others).</summary>
public sealed record AiProvider(
    string Id,
    string DisplayName,
    string? ApiKey,
    string Model,
    string BaseUrl)
{
    /// <summary>True when this provider has an API key and can actually be used.</summary>
    public bool HasKey => !string.IsNullOrWhiteSpace(ApiKey);
}

/// <summary>
/// Minimal configuration loaded from appsettings.json + appsettings.Local.json
/// with environment-variable overrides. Supports several AI providers so the
/// user can switch models (e.g. Groq or OpenAI/ChatGPT) at runtime. Keeps config
/// dependency-free while keeping secrets outside source control. AI is optional.
/// </summary>
public sealed class AppConfig
{
    /// <summary>All configured providers, in display order.</summary>
    public IReadOnlyList<AiProvider> Providers { get; init; } = Array.Empty<AiProvider>();

    /// <summary>Id of the provider used when the caller does not choose one.</summary>
    public string DefaultProviderId { get; init; } = "gemini";

    /// <summary>Providers that have an API key configured (usable right now).</summary>
    public IReadOnlyList<AiProvider> EnabledProviders =>
        Providers.Where(p => p.HasKey).ToList();

    /// <summary>True when at least one provider has a REAL key. The local Ollama
    /// placeholder key does not count, so a fresh keyless download reports AI off
    /// (and prompts for a Gemini key) instead of pretending AI is ready.</summary>
    public bool HasAnyAi => Providers.Any(p => p.HasKey &&
        !string.Equals(p.Id, "ollama", StringComparison.OrdinalIgnoreCase));

    /// <summary>Resolves a provider by id, preferring one that has a key. Falls
    /// back to the default provider, then the first usable one.</summary>
    public AiProvider GetProvider(string? id)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            var byId = Providers.FirstOrDefault(p =>
                string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
            if (byId is { HasKey: true })
            {
                return byId;
            }
        }

        var def = Providers.FirstOrDefault(p =>
            string.Equals(p.Id, DefaultProviderId, StringComparison.OrdinalIgnoreCase));
        if (def is { HasKey: true })
        {
            return def;
        }

        // A real cloud provider the user actually configured. Ignore the local
        // Ollama placeholder key so a fresh, keyless download shows the intended
        // default (Gemini) instead of silently switching to Ollama.
        var realEnabled = EnabledProviders.FirstOrDefault(p =>
            !string.Equals(p.Id, "ollama", StringComparison.OrdinalIgnoreCase));
        if (realEnabled is not null)
        {
            return realEnabled;
        }

        // Nothing is keyed: show the intended default so the banner tells the
        // user which key to add. Fall back to Ollama / any provider last.
        return def
               ?? EnabledProviders.FirstOrDefault()
               ?? Providers.FirstOrDefault()
               ?? new AiProvider("none", "None", null, string.Empty, string.Empty);
    }

    // Preferred models for the coding AGENT (scaffolding real projects): strong
    // coders first, then Google's smarter 2.5 Flash, ending at plain Flash. The
    // first one that has a real key wins, so it works with whatever key is set.
    private static readonly string[] CodingPreferred =
    {
        "tokenrouter-claude-sonnet", "tokenrouter-claude-opus",
        "openrouter-deepseek", "openrouter-qwen-coder",
        "groq-gptoss", "nvidia",
        "gemini-flash25", "gemini-pro", "gemini",
    };

    // Preferred models for ASK mode (fast Q&A): quick, light models first.
    private static readonly string[] FastPreferred =
    {
        "groq", "nvidia-nano", "gemini", "gemini-flash25",
    };

    /// <summary>Best available model for the coding agent. Honours an explicit
    /// choice when it has a key, else picks the first keyed coder.</summary>
    public AiProvider GetCodingProvider(string? chosen = null) =>
        ResolvePreferred(chosen, CodingPreferred);

    /// <summary>Fastest available model for Ask mode. Honours an explicit choice
    /// when it has a key, else picks the first keyed fast model.</summary>
    public AiProvider GetFastProvider(string? chosen = null) =>
        ResolvePreferred(chosen, FastPreferred);

    private AiProvider ResolvePreferred(string? chosen, string[] preferred)
    {
        if (!string.IsNullOrWhiteSpace(chosen))
        {
            var picked = Providers.FirstOrDefault(p =>
                string.Equals(p.Id, chosen, StringComparison.OrdinalIgnoreCase));
            if (picked is { HasKey: true })
            {
                return picked;
            }
        }

        foreach (var id in preferred)
        {
            var p = Providers.FirstOrDefault(x =>
                string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
            if (p is { HasKey: true })
            {
                return p;
            }
        }

        // No preferred model is keyed: fall back to the normal default (which
        // still names Gemini even when no key is set yet).
        return GetProvider(chosen);
    }

    // ---- Backward-compatible accessors for callers that don't switch models. ----
    // They target the resolved default/active provider.
    private AiProvider Active => GetProvider(DefaultProviderId);

    public string? OpenAiApiKey => Active.ApiKey;

    public string OpenAiModel => Active.Model;

    public string OpenAiBaseUrl => Active.BaseUrl;

    public bool HasOpenAi => HasAnyAi;

    public static AppConfig Load(string projectRoot)
    {
        // Built-in defaults so the app runs even with no settings files present.
        var byId = new Dictionary<string, AiProvider>(StringComparer.OrdinalIgnoreCase)
        {
            ["groq"] = new(
                "groq", "Groq \u00b7 Llama 3.3 70B (free)", null,
                "llama-3.3-70b-versatile", "https://api.groq.com/openai/v1/chat/completions"),
            ["groq-compound"] = new(
                "groq-compound", "Groq \u00b7 Compound (web-connected)", null,
                "groq/compound", "https://api.groq.com/openai/v1/chat/completions"),
            ["groq-gptoss"] = new(
                "groq-gptoss", "Groq \u00b7 GPT-OSS 120B (free)", null,
                "openai/gpt-oss-120b", "https://api.groq.com/openai/v1/chat/completions"),
            ["gemini"] = new(
                "gemini", "Google Gemini \u00b7 2.0 Flash (free)", null,
                "gemini-2.0-flash",
                "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions"),
            ["gemini-pro"] = new(
                "gemini-pro", "Google Gemini \u00b7 2.5 Pro (best quality)", null,
                "gemini-2.5-pro",
                "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions"),
            ["gemini-flash25"] = new(
                "gemini-flash25", "Google Gemini \u00b7 2.5 Flash (fast)", null,
                "gemini-2.5-flash",
                "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions"),
            ["openrouter"] = new(
                "openrouter", "OpenRouter \u00b7 Llama 3.3 70B (free)", null,
                "meta-llama/llama-3.3-70b-instruct:free",
                "https://openrouter.ai/api/v1/chat/completions"),
            ["openrouter-gemma"] = new(
                "openrouter-gemma", "OpenRouter \u00b7 Google Gemma 3 27B (free)", null,
                "google/gemma-3-27b-it:free",
                "https://openrouter.ai/api/v1/chat/completions"),
            ["openrouter-deepseek"] = new(
                "openrouter-deepseek", "OpenRouter \u00b7 DeepSeek V3 (free, coding)", null,
                "deepseek/deepseek-chat-v3-0324:free",
                "https://openrouter.ai/api/v1/chat/completions"),
            ["openrouter-deepseek-r1"] = new(
                "openrouter-deepseek-r1", "OpenRouter \u00b7 DeepSeek R1 (free, reasoning)", null,
                "deepseek/deepseek-r1:free",
                "https://openrouter.ai/api/v1/chat/completions"),
            ["openrouter-qwen-coder"] = new(
                "openrouter-qwen-coder", "OpenRouter \u00b7 Qwen 2.5 Coder 32B (free, coding)", null,
                "qwen/qwen-2.5-coder-32b-instruct:free",
                "https://openrouter.ai/api/v1/chat/completions"),
            ["openrouter-mistral"] = new(
                "openrouter-mistral", "OpenRouter \u00b7 Mistral Small 3.1 24B (free)", null,
                "mistralai/mistral-small-3.1-24b-instruct:free",
                "https://openrouter.ai/api/v1/chat/completions"),
            ["openrouter-gptoss"] = new(
                "openrouter-gptoss", "OpenRouter \u00b7 OpenAI gpt-oss 20B (free)", null,
                "openai/gpt-oss-20b:free",
                "https://openrouter.ai/api/v1/chat/completions"),
            ["openrouter-ling"] = new(
                "openrouter-ling", "OpenRouter \u00b7 Ling 3.0 Flash (free)", null,
                "inclusionai/ling-3.0-flash:free",
                "https://openrouter.ai/api/v1/chat/completions"),
            ["openrouter-laguna"] = new(
                "openrouter-laguna", "OpenRouter \u00b7 Laguna S 2.1 (free, coding)", null,
                "poolside/laguna-s-2.1:free",
                "https://openrouter.ai/api/v1/chat/completions"),
            ["openrouter-north-code"] = new(
                "openrouter-north-code", "OpenRouter \u00b7 Cohere North Mini Code (free, coding)", null,
                "cohere/north-mini-code:free",
                "https://openrouter.ai/api/v1/chat/completions"),
            ["tokenrouter"] = new(
                "tokenrouter", "TokenRouter \u00b7 Kimi K3 (free)", null,
                "moonshotai/kimi-k3-free",
                "https://api.tokenrouter.com/v1/chat/completions"),
            ["tokenrouter-kimi"] = new(
                "tokenrouter-kimi", "TokenRouter \u00b7 Kimi K3 (long-context)", null,
                "moonshotai/kimi-k3",
                "https://api.tokenrouter.com/v1/chat/completions"),
            ["tokenrouter-kimi-code"] = new(
                "tokenrouter-kimi-code", "TokenRouter \u00b7 Kimi K2.7 Code", null,
                "moonshotai/kimi-k2.7-code",
                "https://api.tokenrouter.com/v1/chat/completions"),
            ["tokenrouter-claude-sonnet"] = new(
                "tokenrouter-claude-sonnet", "TokenRouter \u00b7 Claude Sonnet 5", null,
                "anthropic/claude-sonnet-5",
                "https://api.tokenrouter.com/v1/chat/completions"),
            ["tokenrouter-claude-opus"] = new(
                "tokenrouter-claude-opus", "TokenRouter \u00b7 Claude Opus 5 (best)", null,
                "anthropic/claude-opus-5",
                "https://api.tokenrouter.com/v1/chat/completions"),
            ["tokenrouter-gpt"] = new(
                "tokenrouter-gpt", "TokenRouter \u00b7 GPT 5.6 (Sol)", null,
                "openai/gpt-5.6-sol",
                "https://api.tokenrouter.com/v1/chat/completions"),
            ["tokenrouter-gpt-fast"] = new(
                "tokenrouter-gpt-fast", "TokenRouter \u00b7 GPT 5.6 (Luna, cheap)", null,
                "openai/gpt-5.6-luna",
                "https://api.tokenrouter.com/v1/chat/completions"),
            ["tokenrouter-gemini"] = new(
                "tokenrouter-gemini", "TokenRouter \u00b7 Gemini 3.6 Flash", null,
                "google/gemini-3.6-flash",
                "https://api.tokenrouter.com/v1/chat/completions"),
            ["tokenrouter-grok"] = new(
                "tokenrouter-grok", "TokenRouter \u00b7 Grok 4.5", null,
                "x-ai/grok-4.5",
                "https://api.tokenrouter.com/v1/chat/completions"),
            ["tokenrouter-deepseek"] = new(
                "tokenrouter-deepseek", "TokenRouter \u00b7 DeepSeek V4 Flash", null,
                "deepseek/deepseek-v4-flash-0731",
                "https://api.tokenrouter.com/v1/chat/completions"),
            ["tokenrouter-glm"] = new(
                "tokenrouter-glm", "TokenRouter \u00b7 GLM 5.2", null,
                "z-ai/glm-5.2",
                "https://api.tokenrouter.com/v1/chat/completions"),
            ["tokenrouter-qwen"] = new(
                "tokenrouter-qwen", "TokenRouter \u00b7 Qwen 3.7 Plus", null,
                "qwen/qwen3.7-plus",
                "https://api.tokenrouter.com/v1/chat/completions"),
            ["tokenrouter-mistral"] = new(
                "tokenrouter-mistral", "TokenRouter \u00b7 Mistral Small", null,
                "mistralai/mistral-small-2603",
                "https://api.tokenrouter.com/v1/chat/completions"),
            ["nvidia"] = new(
                "nvidia", "NVIDIA \u00b7 Nemotron Super 49B (best, fast)", null,
                "nvidia/llama-3.3-nemotron-super-49b-v1.5",
                "https://integrate.api.nvidia.com/v1/chat/completions"),
            ["nvidia-nano"] = new(
                "nvidia-nano", "NVIDIA \u00b7 Nemotron Nano 9B (fastest)", null,
                "nvidia/nvidia-nemotron-nano-9b-v2",
                "https://integrate.api.nvidia.com/v1/chat/completions"),
            ["nvidia-llama70b"] = new(
                "nvidia-llama70b", "NVIDIA \u00b7 Llama 3.1 70B (bigger, slower)", null,
                "meta/llama-3.1-70b-instruct",
                "https://integrate.api.nvidia.com/v1/chat/completions"),
            ["nvidia-deepseek"] = new(
                "nvidia-deepseek", "NVIDIA \u00b7 DeepSeek V4 Flash", null,
                "deepseek-ai/deepseek-v4-flash",
                "https://integrate.api.nvidia.com/v1/chat/completions"),
            ["ollama"] = new(
                "ollama", "Ollama \u00b7 Llama 3.1 (local, no key)", "ollama",
                "llama3.1", "http://localhost:11434/v1/chat/completions"),
            ["openai"] = new(
                "openai", "OpenAI \u00b7 GPT-4o mini (ChatGPT API)", null,
                "gpt-4o-mini", "https://api.openai.com/v1/chat/completions"),
        };
        var order = new List<string> { "groq", "groq-compound", "groq-gptoss", "gemini", "gemini-pro", "gemini-flash25", "openrouter", "openrouter-gemma", "openrouter-deepseek", "openrouter-deepseek-r1", "openrouter-qwen-coder", "openrouter-mistral", "openrouter-gptoss", "openrouter-ling", "openrouter-laguna", "openrouter-north-code", "tokenrouter", "tokenrouter-kimi", "tokenrouter-kimi-code", "tokenrouter-claude-sonnet", "tokenrouter-claude-opus", "tokenrouter-gpt", "tokenrouter-gpt-fast", "tokenrouter-gemini", "tokenrouter-grok", "tokenrouter-deepseek", "tokenrouter-glm", "tokenrouter-qwen", "tokenrouter-mistral", "nvidia", "nvidia-nano", "nvidia-llama70b", "nvidia-deepseek", "ollama", "openai" };
        var defaultId = "gemini";

        // Build the list of config files to read, in priority order (later files
        // win). We always read the files next to the app first, then a fixed
        // per-user folder (%USERPROFILE%\.krishnaagent). That lets the user set
        // their API key ONCE in the home folder and have every copy of the exe
        // — no matter where it lives — pick it up automatically. No copying, no
        // remembering the key each time.
        var searchPaths = new List<string>
        {
            Path.Combine(projectRoot, "appsettings.json"),
            Path.Combine(projectRoot, "appsettings.Local.json"),
        };
        try
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(home))
            {
                var homeDir = Path.Combine(home, ".krishnaagent");
                searchPaths.Add(Path.Combine(homeDir, "appsettings.json"));
                searchPaths.Add(Path.Combine(homeDir, "appsettings.Local.json"));
            }
        }
        catch
        {
            // Ignore if the home folder can't be resolved; app-local files still work.
        }

        foreach (var path in searchPaths)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                var root = doc.RootElement;

                // Preferred: multi-provider "AiProviders" section.
                if (root.TryGetProperty("AiProviders", out var ap))
                {
                    if (TryGetString(ap, "Default", out var d))
                    {
                        defaultId = d;
                    }

                    if (ap.TryGetProperty("Options", out var opts) &&
                        opts.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var o in opts.EnumerateArray())
                        {
                            if (!TryGetString(o, "Id", out var id))
                            {
                                continue;
                            }

                            byId.TryGetValue(id, out var existing);
                            var display = existing?.DisplayName ?? id;
                            var model = existing?.Model ?? string.Empty;
                            var url = existing?.BaseUrl ?? string.Empty;
                            var key = existing?.ApiKey;

                            if (TryGetString(o, "DisplayName", out var dn)) display = dn;
                            if (TryGetString(o, "Model", out var m)) model = m;
                            if (TryGetString(o, "BaseUrl", out var b)) url = b;
                            if (TryGetString(o, "ApiKey", out var k)) key = k;

                            if (existing is null &&
                                !order.Any(x => string.Equals(x, id, StringComparison.OrdinalIgnoreCase)))
                            {
                                order.Add(id);
                            }

                            byId[id] = new AiProvider(id, display, key, model, url);
                        }
                    }
                }

                // Legacy single "OpenAi" section (older config). Its key maps to
                // whichever provider matches its BaseUrl host; historically Groq.
                if (root.TryGetProperty("OpenAi", out var oa))
                {
                    var legacyUrl = TryGetString(oa, "BaseUrl", out var lu) ? lu : null;
                    var targetId =
                        legacyUrl is null ? "groq"
                        : legacyUrl.Contains("groq", StringComparison.OrdinalIgnoreCase) ? "groq"
                        : legacyUrl.Contains("openai", StringComparison.OrdinalIgnoreCase) ? "openai"
                        : defaultId;

                    byId.TryGetValue(targetId, out var ex);
                    var display = ex?.DisplayName ?? targetId;
                    var model = ex?.Model ?? string.Empty;
                    var url = ex?.BaseUrl ?? string.Empty;
                    var key = ex?.ApiKey;

                    if (TryGetString(oa, "Model", out var m2)) model = m2;
                    if (legacyUrl is not null) url = legacyUrl;
                    if (TryGetString(oa, "ApiKey", out var k2)) key = k2;

                    byId[targetId] = new AiProvider(targetId, display, key, model, url);
                }
            }
            catch (JsonException)
            {
                // Ignore a malformed settings file and fall back to defaults.
            }
        }

        // Environment-variable overrides (handy for hosting).
        ApplyEnvKey(byId, "groq", "GROQ_API_KEY");
        ApplyEnvKey(byId, "gemini", "GEMINI_API_KEY");
        ApplyEnvKey(byId, "openrouter", "OPENROUTER_API_KEY");
        ApplyEnvKey(byId, "tokenrouter", "TOKENROUTER_API_KEY");
        ApplyEnvKey(byId, "nvidia", "NVIDIA_API_KEY");
        ApplyEnvKey(byId, "openai", "OPENAI_API_KEY");
        var legacyEnv = Environment.GetEnvironmentVariable("OpenAi__ApiKey");
        if (!string.IsNullOrWhiteSpace(legacyEnv) && byId.TryGetValue("groq", out var g))
        {
            byId["groq"] = g with { ApiKey = legacyEnv };
        }

        // One NVIDIA key powers every NVIDIA model. Share the "nvidia" key (or
        // the NVIDIA_API_KEY env var) with the other nvidia-* models so the user
        // only configures the key once.
        var nvidiaEnv = Environment.GetEnvironmentVariable("NVIDIA_API_KEY");
        var nvidiaKey = !string.IsNullOrWhiteSpace(nvidiaEnv)
            ? nvidiaEnv
            : (byId.TryGetValue("nvidia", out var nv) ? nv.ApiKey : null);
        if (!string.IsNullOrWhiteSpace(nvidiaKey))
        {
            foreach (var id in byId.Keys.Where(k =>
                         k.StartsWith("nvidia", StringComparison.OrdinalIgnoreCase)).ToList())
            {
                if (string.IsNullOrWhiteSpace(byId[id].ApiKey))
                {
                    byId[id] = byId[id] with { ApiKey = nvidiaKey };
                }
            }
        }

        // Likewise, one Groq key powers every Groq model (groq, groq-compound,
        // groq-gptoss). Share the "groq" key with the other groq-* models.
        var groqEnv = Environment.GetEnvironmentVariable("GROQ_API_KEY");
        var groqKey = !string.IsNullOrWhiteSpace(groqEnv)
            ? groqEnv
            : (byId.TryGetValue("groq", out var gq) ? gq.ApiKey : null);
        if (!string.IsNullOrWhiteSpace(groqKey))
        {
            foreach (var id in byId.Keys.Where(k =>
                         k.StartsWith("groq", StringComparison.OrdinalIgnoreCase)).ToList())
            {
                if (string.IsNullOrWhiteSpace(byId[id].ApiKey))
                {
                    byId[id] = byId[id] with { ApiKey = groqKey };
                }
            }
        }

        // One Gemini key powers every Gemini model (gemini flash, gemini-pro,
        // gemini-flash25). Share the "gemini" key with the other gemini-* models
        // so the user only pastes their Google AI Studio key once.
        var geminiEnv = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        var geminiKey = !string.IsNullOrWhiteSpace(geminiEnv)
            ? geminiEnv
            : (byId.TryGetValue("gemini", out var gm) ? gm.ApiKey : null);
        if (!string.IsNullOrWhiteSpace(geminiKey))
        {
            foreach (var id in byId.Keys.Where(k =>
                         k.StartsWith("gemini", StringComparison.OrdinalIgnoreCase)).ToList())
            {
                if (string.IsNullOrWhiteSpace(byId[id].ApiKey))
                {
                    byId[id] = byId[id] with { ApiKey = geminiKey };
                }
            }
        }

        // One OpenRouter key powers every OpenRouter model (openrouter,
        // openrouter-gemma, openrouter-deepseek, openrouter-qwen-coder, ...).
        // Share the "openrouter" key with the other openrouter-* models so the
        // user only pastes their OpenRouter key once. When one free model hits
        // its token/quota limit, AnswerAsync automatically falls back to the
        // next enabled model, so switching happens on its own.
        var openrouterEnv = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
        var openrouterKey = !string.IsNullOrWhiteSpace(openrouterEnv)
            ? openrouterEnv
            : (byId.TryGetValue("openrouter", out var orr) ? orr.ApiKey : null);
        if (!string.IsNullOrWhiteSpace(openrouterKey))
        {
            foreach (var id in byId.Keys.Where(k =>
                         k.StartsWith("openrouter", StringComparison.OrdinalIgnoreCase)).ToList())
            {
                if (string.IsNullOrWhiteSpace(byId[id].ApiKey))
                {
                    byId[id] = byId[id] with { ApiKey = openrouterKey };
                }
            }
        }

        // One TokenRouter key powers every TokenRouter model (tokenrouter,
        // tokenrouter-opus, tokenrouter-haiku, tokenrouter-glm). Share the
        // "tokenrouter" key (or the TOKENROUTER_API_KEY env var) with the other
        // tokenrouter-* models so the user only pastes their key once.
        var tokenrouterEnv = Environment.GetEnvironmentVariable("TOKENROUTER_API_KEY");
        var tokenrouterKey = !string.IsNullOrWhiteSpace(tokenrouterEnv)
            ? tokenrouterEnv
            : (byId.TryGetValue("tokenrouter", out var trr) ? trr.ApiKey : null);
        if (!string.IsNullOrWhiteSpace(tokenrouterKey))
        {
            foreach (var id in byId.Keys.Where(k =>
                         k.StartsWith("tokenrouter", StringComparison.OrdinalIgnoreCase)).ToList())
            {
                if (string.IsNullOrWhiteSpace(byId[id].ApiKey))
                {
                    byId[id] = byId[id] with { ApiKey = tokenrouterKey };
                }
            }
        }

        var providers = order
            .Where(byId.ContainsKey)
            .Select(id => byId[id])
            .ToList();

        return new AppConfig { Providers = providers, DefaultProviderId = defaultId };
    }

    private static void ApplyEnvKey(Dictionary<string, AiProvider> map, string id, string envName)
    {
        var value = Environment.GetEnvironmentVariable(envName);
        if (!string.IsNullOrWhiteSpace(value) && map.TryGetValue(id, out var p))
        {
            map[id] = p with { ApiKey = value };
        }
    }

    private static bool TryGetString(JsonElement parent, string name, out string value)
    {
        value = string.Empty;
        if (parent.TryGetProperty(name, out var el) &&
            el.ValueKind == JsonValueKind.String)
        {
            var s = el.GetString();
            if (!string.IsNullOrWhiteSpace(s))
            {
                value = s;
                return true;
            }
        }

        return false;
    }
}
