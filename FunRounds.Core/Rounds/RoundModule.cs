using System;
using System.Collections.Generic;
using FunRounds.Config;
using FunRounds.Plugins;
using FunRounds.Shared;
using FunRounds.Utils;
using Microsoft.Extensions.Logging;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.GameEvents;
using Sharp.Shared.HookParams;
using Sharp.Shared.Objects;
using Sharp.Shared.Listeners;
using Sharp.Shared.Types;
using Sharp.Shared.Units;

namespace FunRounds.Rounds;

/// <summary>
/// Handles the round lifecycle:
///   OnRoundRestarted (IGameListener) — if auto-random, select a random fun round.
///   round_poststart  (IEventListener) — strip + arm + set health on every alive player,
///                                       then invoke the round's onApply delegate if present.
///   round_end        (IEventListener) — invoke onRevert delegate if present, then clear Current.
///   PlayerDispatchTraceAttack pre-hook — enforce HeadshotOnly / OneTap damage rules.
///   GameFrame hook (pre) — force-unscope every alive player during a NoScope round, every tick.
/// </summary>
internal sealed class RoundModule : IModule, IEventListener, IGameListener
{
    private readonly ILogger<RoundModule> _logger;
    private readonly InterfaceBridge      _bridge;
    private readonly ConfigModule         _config;
    private readonly FunRoundService      _service;

    // Cached delegates for symmetric install/remove
    private readonly Func<IPlayerDispatchTraceAttackHookParams,
                          HookReturnValue<long>,
                          HookReturnValue<long>> _traceAttackPre;

    private readonly Action<bool, bool, bool> _gameFramePre;

    // WeaponLimit override tracking — true when we called SetOverride this round.
    private bool _wlOverridden;

    // ── IEventListener ────────────────────────────────────────────────────
    int IEventListener.ListenerVersion  => IEventListener.ApiVersion;
    int IEventListener.ListenerPriority => 0;

    // ── IGameListener ─────────────────────────────────────────────────────
    int IGameListener.ListenerVersion  => IGameListener.ApiVersion;
    int IGameListener.ListenerPriority => 0;

    public RoundModule(
        ILogger<RoundModule> logger,
        InterfaceBridge      bridge,
        ConfigModule         config,
        FunRoundService      service)
    {
        _logger  = logger;
        _bridge  = bridge;
        _config  = config;
        _service = service;

        _traceAttackPre = OnTraceAttackPre;
        _gameFramePre   = OnGameFramePre;
    }

    // ── IModule lifecycle ──────────────────────────────────────────────────

    public bool Init() => true;

    public void OnPostInit()
    {
        _bridge.EventManager.HookEvent("round_poststart");
        _bridge.EventManager.HookEvent("round_end");
        _bridge.EventManager.InstallEventListener(this);

        _bridge.HookManager.PlayerDispatchTraceAttack.InstallHookPre(_traceAttackPre);

        // Per-tick force-unscope while a NoScope round is active — installed for the
        // lifetime of the plugin (cheap no-op check when no NoScope round is current).
        _bridge.ModSharp.InstallGameFrameHook(_gameFramePre, null);

        _bridge.ModSharp.InstallGameListener(this);
    }

    public void OnAllSharpModulesLoaded() { }

    public void Shutdown()
    {
        _bridge.EventManager.RemoveEventListener(this);
        _bridge.HookManager.PlayerDispatchTraceAttack.RemoveHookPre(_traceAttackPre);
        _bridge.ModSharp.RemoveGameFrameHook(_gameFramePre, null);
        _bridge.ModSharp.RemoveGameListener(this);
    }

    // ── IGameListener — OnRoundRestarted ─────────────────────────────────

    /// <summary>
    /// Fires at the very start of a new round (field-less trigger).
    /// If auto-random is enabled, select a random registered round now so
    /// round_poststart can apply it.
    /// </summary>
    void IGameListener.OnRoundRestarted()
    {
        // An admin-forced round (via command/console) takes priority over the random-chance roll.
        var forced = _service.DequeueForced();
        if (forced is not null && _service.StartRound(forced))
        {
            var f = _service.Current!;
            _logger.LogInformation("[FunRounds] Forced fun round: '{Name}'.", f.Name);
            if (_config.Config.AnnounceRound)
                Loc.ChatAll(_bridge.LocalizerManager, _bridge.ClientManager, "FunRounds_RoundSelected", f.Name);
            SetWeaponLimitOverride(f);
            return;
        }

        var chance = _config.Config.FunRoundChance;
        if (chance <= 0)                    return;   // auto fun rounds off (command-only)
        if (_service.Registered.Count == 0) return;
        if (Random.Shared.Next(100) >= chance) return; // rolled a normal round this time

        var pick = _service.PickRandom();
        if (pick is null) return;

        _logger.LogInformation("[FunRounds] Fun round rolled ({Chance}%): '{Name}'.", chance, pick.Name);

        if (_config.Config.AnnounceRound)
        {
            Loc.ChatAll(_bridge.LocalizerManager, _bridge.ClientManager,
                "FunRounds_RoundSelected", pick.Name);
        }

        SetWeaponLimitOverride(pick);
    }

    /// <summary>
    /// Tells WeaponLimit to enforce the fun-round weapon set instead of its config set.
    /// PlayerSpawnPost fires after OnRoundRestarted, so suspending here is the correct timing.
    /// </summary>
    private void SetWeaponLimitOverride(FunRoundDefinition round)
    {
        if (_wlOverridden || _bridge.WeaponLimit is not { } wl) return;
        wl.SetOverride(round.GetWeapons());
        _wlOverridden = true;
    }

    // ── IEventListener — FireGameEvent ────────────────────────────────────

    void IEventListener.FireGameEvent(IGameEvent @event)
    {
        switch (@event.Name)
        {
            case "round_poststart":
                ApplyCurrentRound();
                break;

            case "round_end":
                InvokeRevert();
                // Revert WeaponLimit to config-defined set BEFORE stopping the round
                // so it goes into effect on the next spawn cycle.
                if (_wlOverridden)
                {
                    _bridge.WeaponLimit?.ClearOverride();
                    _wlOverridden = false;
                }
                // Always clear Current — the next round rolls fresh in OnRoundRestarted, so
                // without this a fun round would persist into the following (normal) round.
                _service.StopRound();
                break;
        }
    }

    // ── Round application ─────────────────────────────────────────────────

    private void ApplyCurrentRound()
    {
        var round = _service.Current;
        if (round is null) return;

        var weapons = round.GetWeapons();
        var applyHp = round.Health != 100;
        var count   = 0;

        // Collect alive player slots for onApply delegate.
        var aliveSlots = new List<PlayerSlot>();

        foreach (var client in _bridge.ClientManager.GetGameClients(inGame: true))
        {
            if (client.IsFakeClient) continue;
            if (!client.IsInGame)    continue;

            var controller = client.GetPlayerController();
            if (controller is not { } ctrl || !ctrl.IsValid()) continue;

            var pawn = ctrl.GetPlayerPawn();
            if (pawn is not { IsAlive: true }) continue;

            // Strip all current weapons (keep suit for armor logic)
            pawn.RemoveAllItems(removeSuit: false);

            // Give each weapon defined for this round
            foreach (var weapon in weapons)
                pawn.GiveNamedItem(weapon);

            // Override health if the round specifies a non-default value
            if (applyHp)
                pawn.Health = round.Health;

            aliveSlots.Add(ctrl.PlayerSlot);
            count++;
        }

        _logger.LogInformation("[FunRounds] Applied '{Name}' to {Count} player(s).", round.Name, count);

        // Prominent win-panel reveal so players notice a fun round is live (self-hides after N s).
        // Text is localized per-client (round.Name is the {0} arg); the panel colour is styling.
        if (_config.Config.AnnounceRound && count > 0)
        {
            var lm = _bridge.LocalizerManager;
            WinPanel.ShowTimed(_bridge,
                c => WinPanel.Format(Loc.Text(lm, c, "FunRounds_RoundReveal", round.Name), "#ffb400"),
                _config.Config.AnnounceSeconds);
        }

        // Invoke optional code-round onApply delegate.
        var (onApply, _) = _service.GetCurrentCallbacks();
        if (onApply is not null)
        {
            try   { onApply(aliveSlots); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FunRounds] onApply threw for round '{Name}'.", round.Name);
            }
        }
    }

    private void InvokeRevert()
    {
        var round = _service.Current;
        if (round is null) return;

        var (_, onRevert) = _service.GetCurrentCallbacks();
        if (onRevert is null) return;

        // Collect alive player slots for onRevert delegate.
        var aliveSlots = new List<PlayerSlot>();
        foreach (var client in _bridge.ClientManager.GetGameClients(inGame: true))
        {
            if (client.IsFakeClient) continue;
            if (!client.IsInGame)    continue;

            var controller = client.GetPlayerController();
            if (controller is not { } ctrl || !ctrl.IsValid()) continue;

            var pawn = ctrl.GetPlayerPawn();
            if (pawn is not { IsAlive: true }) continue;

            aliveSlots.Add(ctrl.PlayerSlot);
        }

        try   { onRevert(aliveSlots); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FunRounds] onRevert threw for round '{Name}'.", round.Name);
        }
    }

    // ── Damage hook ───────────────────────────────────────────────────────

    private HookReturnValue<long> OnTraceAttackPre(
        IPlayerDispatchTraceAttackHookParams p,
        HookReturnValue<long>                current)
    {
        var round = _service.Current;
        if (round is null) return current;

        var hitGroup = p.HitGroup;

        switch (round.DamageMode)
        {
            // HeadshotOnly: headshot REQUIRED — body/limb shots do zero damage.
            case DamageMode.HeadshotOnly when hitGroup != HitGroupType.Head:
                p.Damage = 0f;
                return new HookReturnValue<long>(EHookAction.Ignored);

            // OneTap: ANY hitgroup is a guaranteed kill — one bullet, any location.
            case DamageMode.OneTap:
                p.Damage = 9999f;
                return new HookReturnValue<long>(EHookAction.Ignored);
        }

        return current;
    }

    // ── NoScope enforcement (per-tick force-unscope) ────────────────────────

    /// <summary>
    /// Runs every server tick. When a NoScope round is active, forces every alive
    /// player out of scope (m_bIsScoped = false) so zoom is unusable, not merely penalized.
    /// </summary>
    private void OnGameFramePre(bool simulating, bool firstTick, bool lastTick)
    {
        var round = _service.Current;
        if (round is null || !round.NoScope) return;

        foreach (var client in _bridge.ClientManager.GetGameClients(inGame: true))
        {
            if (client.IsFakeClient) continue;

            var controller = client.GetPlayerController();
            if (controller is not { } ctrl || !ctrl.IsValid()) continue;

            var pawn = ctrl.GetPlayerPawn();
            if (pawn is not { IsAlive: true } || !pawn.IsValid()) continue;

            if (pawn.GetNetVar<bool>("m_bIsScoped"))
                pawn.SetNetVar("m_bIsScoped", false);
        }
    }
}
