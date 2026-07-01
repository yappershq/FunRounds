using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using FunRounds.Plugins;
using FunRounds.Rounds;
using Microsoft.Extensions.Logging;

namespace FunRounds.Config;

internal sealed class ConfigModule : IModule
{
    private readonly InterfaceBridge       _bridge;
    private readonly ILogger<ConfigModule> _logger;
    private readonly FunRoundService       _service;

    public FunRoundsConfig Config { get; private set; } = new();

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        WriteIndented = true,
        Converters    = { new JsonStringEnumConverter() },
    };

    public ConfigModule(InterfaceBridge bridge, ILogger<ConfigModule> logger, FunRoundService service)
    {
        _bridge  = bridge;
        _logger  = logger;
        _service = service;
    }

    public bool Init()
    {
        var path = Path.Combine(_bridge.ConfigPath, "funrounds.json");

        if (!File.Exists(path))
        {
            File.WriteAllText(path, JsonSerializer.Serialize(Config, _jsonOpts));
            _logger.LogInformation("[FunRounds] Created default config at {Path}", path);
        }
        else
        {
            var text = File.ReadAllText(path);
            Config = JsonSerializer.Deserialize<FunRoundsConfig>(text, _jsonOpts) ?? new FunRoundsConfig();
            _logger.LogInformation("[FunRounds] Loaded config from {Path} ({Count} config rounds)",
                path, Config.Rounds.Count);
        }

        return true;
    }

    public void OnPostInit()
    {
        // Register config-driven rounds into the service.
        // All Init()s have run by now so the service is fully initialized.
        foreach (var entry in Config.Rounds)
        {
            if (string.IsNullOrWhiteSpace(entry.ShortName))
            {
                _logger.LogWarning("[FunRounds] Config round with empty ShortName skipped (Name='{Name}').", entry.Name);
                continue;
            }
            _service.Register(entry.ToDefinition());
        }

        if (Config.Rounds.Count > 0)
            _logger.LogInformation("[FunRounds] Registered {Count} config-driven round(s).", Config.Rounds.Count);
    }

    public void OnAllSharpModulesLoaded() { }
    public void Shutdown()                { }
}
