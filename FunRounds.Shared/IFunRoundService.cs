using System;
using System.Collections.Generic;
using Sharp.Shared.Units;

namespace FunRounds.Shared;

/// <summary>
/// Public service published by FunRounds.Core in PostInit.
/// External round-pack modules (e.g. a SuperPowers.FunRounds pack) look this up in their
/// OnAllSharpModulesLoaded and call <see cref="Register(FunRoundDefinition)"/> for each
/// data round, or the delegate overload for code/power rounds.
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

    /// <summary>Register a data round definition.  Call from consumer OnAllSharpModulesLoaded.</summary>
    void Register(FunRoundDefinition round);

    /// <summary>
    /// Register a code/power round — data definition plus optional per-round delegates.
    /// <para>
    /// <paramref name="onApply"/> is invoked on all alive <see cref="PlayerSlot"/>s immediately
    /// after the weapon loadout is applied (round_poststart).  Use this to enable a gamemode
    /// effect such as wallhack, speed boost, or reduced gravity.
    /// </para>
    /// <para>
    /// <paramref name="onRevert"/> is invoked on all alive <see cref="PlayerSlot"/>s when the
    /// round ends (round_end) or when StopRound is called explicitly.  Use this to undo the
    /// effect applied in <paramref name="onApply"/>.
    /// </para>
    /// <para>
    /// A SuperPowers.FunRounds pack registers wallhack/gravity rounds via this overload so the
    /// effect wires itself to the round lifecycle without requiring a later API change.
    /// </para>
    /// </summary>
    void Register(FunRoundDefinition round,
                  Action<IReadOnlyList<PlayerSlot>>? onApply,
                  Action<IReadOnlyList<PlayerSlot>>? onRevert);

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
