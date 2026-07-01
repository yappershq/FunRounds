namespace FunRounds.Config;

/// <summary>
/// Persisted configuration for FunRounds, loaded from <c>configs/funrounds/funrounds.json</c>.
/// </summary>
public sealed class FunRoundsConfig
{
    /// <summary>
    /// When true, a random registered round is selected at the start of each round automatically.
    /// When false, rounds are only started via the <c>funround</c> admin command.
    /// </summary>
    public bool AutoRandomRound { get; set; } = false;

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
