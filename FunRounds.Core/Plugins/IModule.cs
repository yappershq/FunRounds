namespace FunRounds.Plugins;

/// <summary>
/// Internal module contract used inside FunRounds.Core's DI container to fan the
/// ModSharp lifecycle out to every cooperating service.
/// </summary>
public interface IModule
{
    /// <summary>Called from the plugin's <c>Init()</c>.</summary>
    bool Init();

    /// <summary>Called from the plugin's <c>PostInit()</c> — register published interfaces here.</summary>
    void OnPostInit();

    /// <summary>Called from the plugin's <c>OnAllModulesLoaded()</c> — resolve cross-plugin interfaces here.</summary>
    void OnAllSharpModulesLoaded();

    /// <summary>Called from the plugin's <c>Shutdown()</c> — remove hooks/listeners here.</summary>
    void Shutdown();
}
