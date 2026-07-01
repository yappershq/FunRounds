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
    /// The admin permission flag required to use <c>funround</c> / <c>funround_stop</c> commands.
    /// Supports any string recognized by IAdminManager (e.g. "@css/generic", "funrounds:manage").
    /// </summary>
    public string AdminFlag { get; set; } = "funrounds:manage";
}
