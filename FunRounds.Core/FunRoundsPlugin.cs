using System;
using System.IO;
using FunRounds.Plugins;
using FunRounds.Rounds;
using FunRounds.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sharp.Modules.LocalizerManager.Shared;
using Sharp.Shared;

namespace FunRounds;

/// <summary>
/// FunRounds — pluggable special-round engine for CS2/ModSharp.
///
/// Lifecycle (honours ModSharp "all PostInits finish before any OAM" guarantee):
///   PostInit           — publish IFunRoundService so external round-pack modules can subscribe in their OAM.
///   OnAllModulesLoaded — resolve optional external interfaces (LocalizerManager, CommandCenter, AdminManager).
/// </summary>
public sealed class FunRoundsPlugin : IModSharpModule
{
    public string DisplayName   => "FunRounds";
    public string DisplayAuthor => "yappershq";

    private readonly IServiceProvider       _provider;
    private readonly ILogger<FunRoundsPlugin> _logger;
    private readonly InterfaceBridge        _bridge;

    public FunRoundsPlugin(
        ISharedSystem   sharedSystem,
        string?         dllPath,
        string?         sharpPath,
        Version?        version,
        IConfiguration? coreConfiguration,
        bool            hotReload)
    {
        ArgumentNullException.ThrowIfNull(sharedSystem);
        ArgumentNullException.ThrowIfNull(sharpPath);

        var loggerFactory = sharedSystem.GetLoggerFactory();
        _bridge = new InterfaceBridge(this, sharedSystem, sharpPath, loggerFactory);

        var services = new ServiceCollection();
        services.AddSingleton(sharedSystem);
        services.AddSingleton(_bridge);
        services.AddSingleton<ILoggerFactory>(loggerFactory);
        services.AddSingleton(typeof(ILogger<>), typeof(LoggerFactoryLogger<>));
        services.AddModules();

        _provider = services.BuildServiceProvider();
        _logger   = _provider.GetRequiredService<ILogger<FunRoundsPlugin>>();
    }

    public bool Init()
    {
        foreach (var module in _provider.GetServices<IModule>())
            CallSafe(module, static m => { m.Init(); }, "Init");
        return true;
    }

    /// <summary>Publish IFunRoundService so external round-pack plugins can subscribe in their OAM.</summary>
    public void PostInit()
    {
        var service = _provider.GetRequiredService<FunRoundService>();
        _bridge.SharpModuleManager
            .RegisterSharpModuleInterface<IFunRoundService>(this, IFunRoundService.Identity, service);

        foreach (var module in _provider.GetServices<IModule>())
            CallSafe(module, static m => m.OnPostInit(), "PostInit");

        _logger.LogInformation("[FunRounds] Published IFunRoundService.");
    }

    public void OnAllModulesLoaded()
    {
        // Resolve optional localization BEFORE modules' OAM runs so their command handlers see it.
        _bridge.LocalizerManager = _bridge.SharpModuleManager
            .GetOptionalSharpModuleInterface<ILocalizerManager>(ILocalizerManager.Identity)?.Instance;
        LoadLocaleFiles();

        foreach (var module in _provider.GetServices<IModule>())
            CallSafe(module, static m => m.OnAllSharpModulesLoaded(), "OnAllModulesLoaded");

        _logger.LogInformation("[FunRounds] All modules loaded.");
    }

    private void LoadLocaleFiles()
    {
        if (_bridge.LocalizerManager is not { } lm)
        {
            _logger.LogInformation("[FunRounds] ILocalizerManager not available — user-facing text will be silent.");
            return;
        }

        var localesPath = Path.Combine(_bridge.SharpPath, "locales");
        if (!Directory.Exists(localesPath)) return;

        foreach (var file in Directory.GetFiles(localesPath, "funrounds*.json"))
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            lm.LoadLocaleFile(fileName);
            _logger.LogInformation("[FunRounds] Loaded locale file: {FileName}", fileName);
        }
    }

    public void Shutdown()
    {
        foreach (var module in _provider.GetServices<IModule>())
            CallSafe(module, static m => m.Shutdown(), "Shutdown");

        if (_provider is IDisposable disposable)
            disposable.Dispose();
    }

    private void CallSafe(IModule module, Action<IModule> action, string phase)
    {
        try   { action(module); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FunRounds] Error in {Phase} for {Module}", phase, module.GetType().Name);
        }
    }
}

/// <summary>Generic logger adapter bridging ILogger&lt;T&gt; onto ModSharp's factory.</summary>
internal sealed class LoggerFactoryLogger<T>(ILoggerFactory factory) : ILogger<T>
{
    private readonly ILogger _inner = factory.CreateLogger(typeof(T).Name);

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _inner.BeginScope(state);
    public bool IsEnabled(LogLevel logLevel) => _inner.IsEnabled(logLevel);
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
        => _inner.Log(logLevel, eventId, state, exception, formatter);
}
