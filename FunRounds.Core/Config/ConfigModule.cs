using System.IO;
using System.Text.Json;
using FunRounds.Plugins;
using Microsoft.Extensions.Logging;

namespace FunRounds.Config;

internal sealed class ConfigModule : IModule
{
    private readonly InterfaceBridge       _bridge;
    private readonly ILogger<ConfigModule> _logger;

    public FunRoundsConfig Config { get; private set; } = new();

    public ConfigModule(InterfaceBridge bridge, ILogger<ConfigModule> logger)
    {
        _bridge = bridge;
        _logger = logger;
    }

    public bool Init()
    {
        var path = Path.Combine(_bridge.ConfigPath, "funrounds.json");
        var opts = new JsonSerializerOptions { WriteIndented = true };

        if (!File.Exists(path))
        {
            File.WriteAllText(path, JsonSerializer.Serialize(Config, opts));
            _logger.LogInformation("[FunRounds] Created default config at {Path}", path);
        }
        else
        {
            var text = File.ReadAllText(path);
            Config = JsonSerializer.Deserialize<FunRoundsConfig>(text, opts) ?? new FunRoundsConfig();
            _logger.LogInformation("[FunRounds] Loaded config from {Path}", path);
        }

        return true;
    }

    public void OnPostInit()              { }
    public void OnAllSharpModulesLoaded() { }
    public void Shutdown()                { }
}
