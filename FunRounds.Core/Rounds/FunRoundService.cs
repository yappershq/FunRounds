using System.Collections.Generic;
using FunRounds.Plugins;
using FunRounds.Shared;
using Microsoft.Extensions.Logging;

namespace FunRounds.Rounds;

/// <summary>
/// Registry + active-round state.  Published as <see cref="IFunRoundService"/> in PostInit.
/// </summary>
internal sealed class FunRoundService : IFunRoundService, IModule
{
    private readonly ILogger<FunRoundService>  _logger;
    private readonly List<FunRoundDefinition>  _rounds = [];
    private readonly Dictionary<string, FunRoundDefinition> _byShort = new(StringComparer.OrdinalIgnoreCase);

    private FunRoundDefinition? _current;

    public FunRoundDefinition? Current => _current;

    public IReadOnlyCollection<FunRoundDefinition> Registered => _rounds;

    public FunRoundService(ILogger<FunRoundService> logger)
    {
        _logger = logger;
    }

    // ── IFunRoundService ───────────────────────────────────────────────────

    public void Register(FunRoundDefinition round)
    {
        if (_byShort.ContainsKey(round.ShortName))
        {
            _logger.LogWarning("[FunRounds] Duplicate shortName '{Short}' — skipping registration of '{Name}'.",
                round.ShortName, round.Name);
            return;
        }
        _rounds.Add(round);
        _byShort[round.ShortName] = round;
        _logger.LogDebug("[FunRounds] Registered round '{Name}' ({Short}).", round.Name, round.ShortName);
    }

    public bool StartRound(string shortName)
    {
        if (!_byShort.TryGetValue(shortName, out var round))
        {
            _logger.LogWarning("[FunRounds] Unknown shortName '{Short}'.", shortName);
            return false;
        }
        _current = round;
        _logger.LogInformation("[FunRounds] Started round '{Name}'.", round.Name);
        return true;
    }

    public void StopRound()
    {
        if (_current is null) return;
        _logger.LogInformation("[FunRounds] Stopped round '{Name}'.", _current.Name);
        _current = null;
    }

    /// <summary>
    /// Picks a random registered round and sets it as current.
    /// Returns null when no rounds are registered.
    /// </summary>
    public FunRoundDefinition? PickRandom()
    {
        if (_rounds.Count == 0) return null;
        var pick = _rounds[Random.Shared.Next(_rounds.Count)];
        _current = pick;
        return pick;
    }

    // ── IModule lifecycle ──────────────────────────────────────────────────

    public bool Init()                   => true;
    public void OnPostInit()             { }
    public void OnAllSharpModulesLoaded(){ }
    public void Shutdown()               { _current = null; }
}
