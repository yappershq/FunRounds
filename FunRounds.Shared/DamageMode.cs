namespace FunRounds.Shared;

/// <summary>
/// Controls how damage is filtered during a fun round.
/// </summary>
public enum DamageMode
{
    /// <summary>No damage restriction — all hits deal normal damage.</summary>
    Any,

    /// <summary>Only headshots deal damage; body/limb shots are suppressed.</summary>
    HeadshotOnly,

    /// <summary>Headshots deal lethal damage (9999); body shots deal normal damage.</summary>
    OneTap,
}
