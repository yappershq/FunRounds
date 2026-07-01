using FunRounds.Commands;
using FunRounds.Config;
using FunRounds.Rounds;
using Microsoft.Extensions.DependencyInjection;

namespace FunRounds.Plugins;

internal static class ModuleDependencyInjection
{
    /// <summary>Register all Core services and modules into the DI container.</summary>
    internal static IServiceCollection AddModules(this IServiceCollection services)
    {
        // Config — must be first so other modules can read it
        services.AddSingleton<ConfigModule>();
        services.AddSingleton<IModule>(sp => sp.GetRequiredService<ConfigModule>());

        // The IFunRoundService implementation — published in PostInit
        services.AddSingleton<FunRoundService>();
        services.AddSingleton<IModule>(sp => sp.GetRequiredService<FunRoundService>());

        // Round lifecycle: weapon apply + damage hooks
        services.AddSingleton<RoundModule>();
        services.AddSingleton<IModule>(sp => sp.GetRequiredService<RoundModule>());

        // Admin/player commands
        services.AddSingleton<CommandsModule>();
        services.AddSingleton<IModule>(sp => sp.GetRequiredService<CommandsModule>());

        return services;
    }
}
