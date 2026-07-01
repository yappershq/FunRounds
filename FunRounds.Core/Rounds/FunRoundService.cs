using System;
using System.Collections.Generic;
using FunRounds.Plugins;
using FunRounds.Shared;
using Microsoft.Extensions.Logging;
using Sharp.Shared.Units;

namespace FunRounds.Rounds;

/// <summary>
/// Registry + active-round state.  Published as <see cref="IFunRoundService"/> in PostInit.
/// </summary>
internal sealed class FunRoundService : IFunRoundService, IModule
{
    private readonly ILogger<FunRoundService> _logger;
    private readonly List<FunRoundDefinition> _rounds = [];
    private readonly Dictionary<string, FunRoundDefinition> _byShort
        = new(StringComparer.OrdinalIgnoreCase);

    // Optional per-round code callbacks (ShortName → delegates).
    private readonly Dictionary<string, (Action<IReadOnlyList<PlayerSlot>>? onApply,
                                          Action<IReadOnlyList<PlayerSlot>>? onRevert)> _callbacks
        = new(StringComparer.OrdinalIgnoreCase);

    private FunRoundDefinition? _current;
    private string?             _pendingForced; // a round queued (by command) to run NEXT round

    public FunRoundDefinition? Current => _current;

    /// <summary>Whether a given short name is a registered round.</summary>
    public bool Has(string shortName) => _byShort.ContainsKey(shortName);

    /// <summary>
    /// Queue a round to start on the NEXT round (overrides the random-chance roll). Returns false
    /// if the short name isn't registered. Used by the admin force commands — a fun round can't be
    /// applied mid-round (loadout happens at round_poststart), so forcing always targets next round.
    /// </summary>
    public bool QueueForced(string shortName)
    {
        if (!_byShort.ContainsKey(shortName)) return false;
        _pendingForced = shortName;
        return true;
    }

    /// <summary>Take + clear any queued forced round (called at round start before the chance roll).</summary>
    public string? DequeueForced()
    {
        var p = _pendingForced;
        _pendingForced = null;
        return p;
    }

    public IReadOnlyCollection<FunRoundDefinition> Registered => _rounds;

    public FunRoundService(ILogger<FunRoundService> logger)
    {
        _logger = logger;
    }

    // ── IFunRoundService ───────────────────────────────────────────────────

    public void Register(FunRoundDefinition round)
        => Register(round, null, null);

    public void Register(FunRoundDefinition round,
                         Action<IReadOnlyList<PlayerSlot>>? onApply,
                         Action<IReadOnlyList<PlayerSlot>>? onRevert)
    {
        if (_byShort.ContainsKey(round.ShortName))
        {
            _logger.LogWarning("[FunRounds] Duplicate shortName '{Short}' — skipping registration of '{Name}'.",
                round.ShortName, round.Name);
            return;
        }
        _rounds.Add(round);
        _byShort[round.ShortName] = round;
        if (onApply is not null || onRevert is not null)
            _callbacks[round.ShortName] = (onApply, onRevert);
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
    /// Picks a random registered round weighted by <see cref="FunRoundDefinition.Weight"/>
    /// and sets it as current.  Returns null when no rounds are registered.
    /// </summary>
    public FunRoundDefinition? PickRandom()
    {
        if (_rounds.Count == 0) return null;

        var totalWeight = 0;
        foreach (var r in _rounds) totalWeight += Math.Max(1, r.Weight);

        var roll = Random.Shared.Next(totalWeight);
        var cumulative = 0;
        FunRoundDefinition? pick = null;
        foreach (var r in _rounds)
        {
            cumulative += Math.Max(1, r.Weight);
            if (roll < cumulative)
            {
                pick = r;
                break;
            }
        }

        pick ??= _rounds[^1]; // safety fallback (shouldn't happen)
        _current = pick;
        return pick;
    }

    /// <summary>
    /// Returns the code callbacks registered for the current round, or (null, null) if none.
    /// </summary>
    internal (Action<IReadOnlyList<PlayerSlot>>? onApply, Action<IReadOnlyList<PlayerSlot>>? onRevert)
        GetCurrentCallbacks()
    {
        if (_current is null) return (null, null);
        return _callbacks.TryGetValue(_current.ShortName, out var cbs) ? cbs : (null, null);
    }

    // ── IModule lifecycle ──────────────────────────────────────────────────

    public bool Init()                    => true;
    public void OnPostInit()              { }
    public void OnAllSharpModulesLoaded() { }
    public void Shutdown()                { _current = null; }
}
