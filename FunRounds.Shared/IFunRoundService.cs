using System.Collections.Generic;

namespace FunRounds.Shared;

/// <summary>
/// Public service published by FunRounds.Core in PostInit.
/// External round-pack modules (e.g. FunRounds.Awp) look this up in their
/// OnAllSharpModulesLoaded and call <see cref="Register"/> for each round they provide.
///
/// Lookup pattern in consumer OAM:
/// <code>
/// var iface   = mgr.GetOptionalSharpModuleInterface&lt;IFunRoundService&gt;(IFunRoundService.Identity);
/// var service = iface?.Instance;
/// service?.Register(new FunRoundBuilder()…Build());
/// </code>
/// </summary>
public interface IFunRoundService
{
    static string Identity => typeof(IFunRoundService).FullName!;

    /// <summary>Register a round definition.  Call from consumer OnAllSharpModulesLoaded.</summary>
    void Register(FunRoundDefinition round);

    /// <summary>Start a fun round by its <see cref="FunRoundDefinition.ShortName"/>.</summary>
    /// <returns>True when the round was found and started; false if the shortName is unknown.</returns>
    bool StartRound(string shortName);

    /// <summary>Clear the active fun round (reverts to normal play next round).</summary>
    void StopRound();

    /// <summary>Currently active fun round, or null during normal play.</summary>
    FunRoundDefinition? Current { get; }

    /// <summary>All registered round definitions, in registration order.</summary>
    IReadOnlyCollection<FunRoundDefinition> Registered { get; }
}
