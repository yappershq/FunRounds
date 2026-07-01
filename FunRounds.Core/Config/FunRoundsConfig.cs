using System.Collections.Generic;
using FunRounds.Shared;

namespace FunRounds.Config;

/// <summary>
/// Persisted configuration for FunRounds, loaded from <c>configs/funrounds/funrounds.json</c>.
/// </summary>
public sealed class FunRoundsConfig
{
    /// <summary>
    /// Percent chance (0–100) that any given round becomes a random fun round. The rest are
    /// normal rounds. 0 disables auto fun rounds entirely (admins can still force one via the
    /// <c>funround</c> command); 100 makes every round a fun round. Default 15 — occasional fun,
    /// mostly normal play.
    /// </summary>
    public int FunRoundChance { get; set; } = 15;

    /// <summary>
    /// When true, a chat message is sent to all players announcing the fun round name.
    /// </summary>
    public bool AnnounceRound { get; set; } = true;

    /// <summary>
    /// How long (seconds) the prominent win-panel round reveal stays on screen before it's hidden.
    /// Only used when <see cref="AnnounceRound"/> is true.
    /// </summary>
    public int AnnounceSeconds { get; set; } = 5;

    /// <summary>
    /// The admin permission flag required to use <c>funround</c> / <c>funround_stop</c> commands.
    /// Supports any string recognized by IAdminManager (e.g. "@css/generic", "funrounds:manage").
    /// </summary>
    public string AdminFlag { get; set; } = "funrounds:manage";

    /// <summary>
    /// Config-driven round definitions. Entries are loaded at startup and registered into
    /// <see cref="IFunRoundService"/> automatically — no DLL round-pack needed for data rounds.
    /// Copy one of the <c>.example.json</c> files from <c>.assets/configs/funrounds/</c> as a
    /// starting point, or add entries manually.
    /// </summary>
    public List<RoundConfigEntry> Rounds { get; set; } = new();
}

/// <summary>
/// JSON-serializable mirror of <see cref="FunRoundDefinition"/> fields.
/// <see cref="DamageMode"/> is stored as a string (e.g. <c>"Any"</c>, <c>"HeadshotOnly"</c>,
/// <c>"OneTap"</c>) so operators can edit the config without knowing numeric enum values.
/// </summary>
public sealed class RoundConfigEntry
{
    /// <summary>Full display name, e.g. "AWP NoScope".</summary>
    public string Name { get; set; } = "";

    /// <summary>Unique short identifier used in commands, e.g. "awp_ns".</summary>
    public string ShortName { get; set; } = "";

    /// <summary>Primary weapon classname, or null if none.</summary>
    public string? PrimaryWeapon { get; set; }

    /// <summary>Secondary weapon classname, or null if none.</summary>
    public string? SecondaryWeapon { get; set; }

    /// <summary>Whether to give a knife (weapon_knife).</summary>
    public bool Knife { get; set; }

    /// <summary>Whether to give a taser (weapon_taser).</summary>
    public bool Taser { get; set; }

    /// <summary>Whether to give an HE grenade (weapon_hegrenade).</summary>
    public bool HeGrenade { get; set; }

    /// <summary>Whether to give a decoy (weapon_decoy).</summary>
    public bool Decoy { get; set; }

    /// <summary>Damage filter for this round. Valid values: "Any", "HeadshotOnly", "OneTap".</summary>
    public DamageMode DamageMode { get; set; } = DamageMode.Any;

    /// <summary>If true, scoped shots are suppressed (damage zeroed).</summary>
    public bool NoScope { get; set; }

    /// <summary>Starting health for players this round.</summary>
    public int Health { get; set; } = 100;

    /// <summary>
    /// Relative weight for random-round selection.  Higher = appears more often.  Default 1.
    /// </summary>
    public int Weight { get; set; } = 1;

    /// <summary>Converts this entry to a <see cref="FunRoundDefinition"/>.</summary>
    public FunRoundDefinition ToDefinition() => new()
    {
        Name            = Name,
        ShortName       = ShortName,
        PrimaryWeapon   = PrimaryWeapon,
        SecondaryWeapon = SecondaryWeapon,
        Knife           = Knife,
        Taser           = Taser,
        HeGrenade       = HeGrenade,
        Decoy           = Decoy,
        DamageMode      = DamageMode,
        NoScope         = NoScope,
        Health          = Health,
        Weight          = Weight,
    };
}
