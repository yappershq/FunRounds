using System.IO;
using Microsoft.Extensions.Logging;
using Sharp.Modules.LocalizerManager.Shared;
using Sharp.Shared;
using Sharp.Shared.Managers;

namespace FunRounds;

/// <summary>
/// Single cached gateway to every ModSharp manager the Core plugin needs.
/// Built once in the plugin ctor; optional external modules resolved in OnAllModulesLoaded.
/// </summary>
internal sealed class InterfaceBridge
{
    // === Paths ===
    internal string SharpPath  { get; }
    internal string ConfigPath { get; }

    // === Managers ===
    internal IEntityManager EntityManager { get; }
    internal IClientManager ClientManager { get; }
    internal IHookManager   HookManager   { get; }
    internal IEventManager  EventManager  { get; }

    // === Services ===
    internal IModSharp           ModSharp           { get; }
    internal ILoggerFactory      LoggerFactory      { get; }
    internal ISharpModuleManager SharpModuleManager { get; }
    internal IModSharpModule     Module             { get; }

    /// <summary>
    /// Optional localization service. Resolved in <c>OnAllModulesLoaded</c>; null when not installed.
    /// </summary>
    internal ILocalizerManager? LocalizerManager { get; set; }

    public InterfaceBridge(IModSharpModule module, ISharedSystem sharedSystem, string sharpPath, ILoggerFactory loggerFactory)
    {
        Module = module;

        SharpPath  = sharpPath;
        ConfigPath = Path.Combine(sharpPath, "configs", "funrounds");

        Directory.CreateDirectory(ConfigPath);

        EntityManager = sharedSystem.GetEntityManager();
        ClientManager = sharedSystem.GetClientManager();
        HookManager   = sharedSystem.GetHookManager();
        EventManager  = sharedSystem.GetEventManager();

        ModSharp           = sharedSystem.GetModSharp();
        LoggerFactory      = loggerFactory;
        SharpModuleManager = sharedSystem.GetSharpModuleManager();
    }
}
