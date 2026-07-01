using System.Collections.Generic;

namespace FunRounds.Shared;

/// <summary>
/// Describes a single fun round variant.  Immutable after construction.
/// Use <see cref="FunRoundBuilder"/> to create instances.
/// </summary>
public sealed class FunRoundDefinition
{
    /// <summary>Full display name, e.g. "AWP NoScope".</summary>
    public string Name { get; init; } = "";

    /// <summary>Unique short identifier used in commands, e.g. "awp_ns".</summary>
    public string ShortName { get; init; } = "";

    /// <summary>Primary weapon classname, or null if none.</summary>
    public string? PrimaryWeapon { get; init; }

    /// <summary>Secondary weapon classname, or null if none.</summary>
    public string? SecondaryWeapon { get; init; }

    /// <summary>Whether to give a knife (weapon_knife).</summary>
    public bool Knife { get; init; }

    /// <summary>Whether to give a taser (weapon_taser).</summary>
    public bool Taser { get; init; }

    /// <summary>Whether to give an HE grenade (weapon_hegrenade).</summary>
    public bool HeGrenade { get; init; }

    /// <summary>Whether to give a decoy (weapon_decoy).</summary>
    public bool Decoy { get; init; }

    /// <summary>Damage filter for this round.</summary>
    public DamageMode DamageMode { get; init; } = DamageMode.Any;

    /// <summary>If true, scoped shots are suppressed (damage zeroed).</summary>
    public bool NoScope { get; init; }

    /// <summary>Starting health for players this round.</summary>
    public int Health { get; init; } = 100;

    /// <summary>
    /// Returns the ordered list of weapon classnames to give a player at round start.
    /// </summary>
    public IReadOnlyList<string> GetWeapons()
    {
        var weapons = new List<string>(6);
        if (PrimaryWeapon is not null)  weapons.Add(PrimaryWeapon);
        if (SecondaryWeapon is not null) weapons.Add(SecondaryWeapon);
        if (Knife)     weapons.Add("weapon_knife");
        if (Taser)     weapons.Add("weapon_taser");
        if (HeGrenade) weapons.Add("weapon_hegrenade");
        if (Decoy)     weapons.Add("weapon_decoy");
        return weapons;
    }
}
