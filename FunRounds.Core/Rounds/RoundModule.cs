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
///   PlayerPreThink forward — NoScope enforcement + DropOnMiss weapon drop.
///   bullet_impact / player_hurt — tick-scoped hit/miss counters for DropOnMiss.
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

    private readonly Action<IPlayerThinkForwardParams> _playerPreThink;

    private readonly Action<IPlayerSpawnForwardParams> _playerSpawnPost;

    // Weapon classnames that can scope — same set as Bara/NoScope's SetNoScope().
    private static readonly HashSet<string> ScopedWeapons = new(StringComparer.OrdinalIgnoreCase)
    {
        "weapon_ssg08", "weapon_awp", "weapon_aug", "weapon_sg556", "weapon_g3sg1", "weapon_scar20",
    };

    // WeaponLimit override tracking — true when we called SetOverride this round.
    private bool _wlOverridden;

    // Pre-round ConVar values, captured so a round's overrides (e.g. bhop) can be reverted exactly.
    private Dictionary<string, string>? _conVarRevert;

    // DropOnMiss tick-scoped counters: bullet_impact always fires per shot (hit or miss);
    // player_hurt only fires on an actual hit. impacts > hits this tick == a shot missed.
    // Reset per-player at the end of that player's own PreThink (see OnPlayerPreThink).
    private static readonly byte MaxSlots = PlayerSlot.MaxPlayerCount.AsPrimitive();
    private readonly int[] _impactsThisTick = new int[MaxSlots];
    private readonly int[] _hitsThisTick    = new int[MaxSlots];

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

        _traceAttackPre  = OnTraceAttackPre;
        _playerPreThink  = OnPlayerPreThink;
        _playerSpawnPost = OnPlayerSpawnPost;
    }

    // ── IModule lifecycle ──────────────────────────────────────────────────

    public bool Init() => true;

    public void OnPostInit()
    {
        _bridge.EventManager.HookEvent("round_poststart");
        _bridge.EventManager.HookEvent("round_end");
        _bridge.EventManager.HookEvent("grenade_thrown");
        _bridge.EventManager.HookEvent("bullet_impact");
        _bridge.EventManager.HookEvent("player_hurt");
        _bridge.EventManager.InstallEventListener(this);

        _bridge.HookManager.PlayerDispatchTraceAttack.InstallHookPre(_traceAttackPre);

        // Belt-and-suspenders vs round_poststart: give the current round's loadout on every
        // individual spawn too (late joiners, mid-round respawns, and any case where a player's
        // pawn isn't alive yet when round_poststart's alive-player snapshot runs).
        _bridge.HookManager.PlayerSpawnPost.InstallForward(_playerSpawnPost);

        // Per-player NoScope enforcement — installed for the lifetime of the plugin (cheap no-op
        // check when no NoScope round is current). Matches Bara/NoScope's SDKHook_PreThink.
        _bridge.HookManager.PlayerPreThink.InstallForward(_playerPreThink);

        _bridge.ModSharp.InstallGameListener(this);
    }

    public void OnAllSharpModulesLoaded() { }

    public void Shutdown()
    {
        _bridge.EventManager.RemoveEventListener(this);
        _bridge.HookManager.PlayerDispatchTraceAttack.RemoveHookPre(_traceAttackPre);
        _bridge.HookManager.PlayerSpawnPost.RemoveForward(_playerSpawnPost);
        _bridge.HookManager.PlayerPreThink.RemoveForward(_playerPreThink);
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
            ApplyRoundConVars(f);
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
        ApplyRoundConVars(pick);
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

    /// <summary>
    /// Applies a round's ConVar overrides (e.g. bhop), capturing each cvar's current value first
    /// so <see cref="RevertRoundConVars"/> can restore it exactly at round end.
    /// </summary>
    private void ApplyRoundConVars(FunRoundDefinition round)
    {
        if (round.ConVars.Count == 0) return;

        _conVarRevert = new Dictionary<string, string>();
        foreach (var (name, value) in round.ConVars)
        {
            var cvar = _bridge.ConVarManager.FindConVar(name);
            if (cvar is null)
            {
                _logger.LogWarning("[FunRounds] ConVar '{Name}' not found — skipping.", name);
                continue;
            }

            _conVarRevert[name] = cvar.GetString();
            cvar.SetString(value);
        }
    }

    private void RevertRoundConVars()
    {
        if (_conVarRevert is null) return;

        foreach (var (name, value) in _conVarRevert)
            _bridge.ConVarManager.FindConVar(name)?.SetString(value);

        _conVarRevert = null;
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
                RevertRoundConVars();
                // Always clear Current — the next round rolls fresh in OnRoundRestarted, so
                // without this a fun round would persist into the following (normal) round.
                _service.StopRound();
                break;

            case "grenade_thrown":
                if (@event is IEventGrenadeThrown thrown)
                    OnGrenadeThrown(thrown);
                break;

            case "bullet_impact":
                OnBulletImpact(@event);
                break;

            case "player_hurt":
                if (@event is IEventPlayerHurt hurt)
                    OnPlayerHurt(hurt);
                break;
        }
    }

    // ── DropOnMiss ────────────────────────────────────────────────────────

    private void OnBulletImpact(IGameEvent @event)
    {
        if (_service.Current is not { DropOnMiss: true }) return;

        var userId = new UserID((ushort) @event.GetInt("userid"));
        var client = _bridge.ClientManager.GetGameClient(userId);
        if (client is null) return;

        _impactsThisTick[client.Slot.AsPrimitive()]++;
    }

    private void OnPlayerHurt(IEventPlayerHurt @event)
    {
        if (_service.Current is not { DropOnMiss: true }) return;

        var attacker = @event.KillerController;
        var victim   = @event.VictimController;
        if (attacker is null || victim is null || attacker.PlayerSlot == victim.PlayerSlot) return;

        _hitsThisTick[attacker.PlayerSlot.AsPrimitive()]++;
    }

    /// <summary>
    /// Re-gives the thrown grenade so a HE+Knife/Decoy War round never runs dry mid-life — matches
    /// the round's own grenade set (HeGrenade → weapon_hegrenade, Decoy → weapon_decoy); ignores
    /// throws of anything else (e.g. a player who somehow still has a flash from a prior round).
    /// </summary>
    private void OnGrenadeThrown(IEventGrenadeThrown @event)
    {
        var round = _service.Current;
        if (round is null) return;

        var isRoundGrenade = @event.Grenade switch
        {
            "hegrenade" => round.HeGrenade,
            "decoy"     => round.Decoy,
            _           => false,
        };
        if (!isRoundGrenade) return;

        if (@event.Pawn is not { IsAlive: true } pawn || !pawn.IsValid()) return;

        // Defer — re-giving on the same tick as the throw can race the engine's own weapon-switch.
        _bridge.ModSharp.InvokeFrameAction(() =>
        {
            if (pawn.IsValid() && pawn.IsAlive && _service.Current == round)
                pawn.GiveNamedItem("weapon_" + @event.Grenade);
        });
    }

    // ── Round application ─────────────────────────────────────────────────

    /// <summary>
    /// Gives round weapons directly. Fallback path ONLY — used when WeaponLimit isn't installed.
    /// When WeaponLimit IS present, its own PlayerSpawnPost hook gives the override set on every
    /// spawn (see WeaponLimitModule.GiveAndRefill); FunRounds must not also strip/give, since that
    /// duplicated pass raced round_poststart's one-shot "alive right now" snapshot and could leave
    /// a player weaponless if their pawn wasn't alive yet at that exact instant.
    /// </summary>
    private static void GiveWeaponsDirect(IPlayerPawn pawn, IReadOnlyList<string> weapons)
    {
        pawn.RemoveAllItems(removeSuit: false);
        foreach (var weapon in weapons)
            pawn.GiveNamedItem(weapon);
    }

    /// <summary>
    /// Applies the current round's health override to a single freshly-spawned pawn (and, when
    /// WeaponLimit isn't installed, the weapons too). Covers late joiners, mid-round respawns, and
    /// any spawn that happens after round_poststart's snapshot already ran.
    /// </summary>
    private void OnPlayerSpawnPost(IPlayerSpawnForwardParams @params)
    {
        var round = _service.Current;
        if (round is null) return;

        if (@params.Pawn is not { } basePawn || !basePawn.IsValid() || !basePawn.IsAlive) return;

        // Defer to next frame — same reasoning as WeaponLimit: removing weapons mid-spawn-forward
        // races the engine's own equip path.
        _bridge.ModSharp.InvokeFrameAction(() =>
        {
            if (!basePawn.IsValid() || !basePawn.IsAlive) return;
            if (basePawn.AsPlayerPawn() is not { } pawn) return;
            if (_service.Current != round) return; // round already changed/ended

            if (_bridge.WeaponLimit is null)
                GiveWeaponsDirect(pawn, round.GetWeapons());

            if (round.Health != 100)
                pawn.Health = round.Health;
        });
    }

    private void ApplyCurrentRound()
    {
        var round = _service.Current;
        if (round is null) return;

        var weapons       = round.GetWeapons();
        var giveDirectly  = _bridge.WeaponLimit is null;
        var count         = 0;

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

            if (giveDirectly)
                GiveWeaponsDirect(pawn, weapons);

            if (round.Health != 100)
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

    // ── Per-tick enforcement (NoScope + DropOnMiss) ──────────────────────────

    /// <summary>
    /// Fires every PreThink for every alive pawn (matches Bara/NoScope's SDKHook_PreThink).
    /// Handles two independent round mechanics, each a no-op when the current round doesn't use it:
    ///   NoScope    — pins the active weapon's secondary-attack cooldown in the future, so the
    ///                scope action itself never fires (the engine's own attack-rate gate blocks
    ///                it) — cheaper and more reliable than fighting client-side scope state after
    ///                the fact (m_bIsScoped resets one tick too late, causing a visible flash).
    ///   DropOnMiss — compares this tick's bullet_impact vs player_hurt counts for this player;
    ///                more impacts than hits means a shot missed, so drop the active weapon.
    /// </summary>
    private void OnPlayerPreThink(IPlayerThinkForwardParams @params)
    {
        var round = _service.Current;
        if (round is null) return;

        if (@params.Pawn is not { IsAlive: true } pawn || !pawn.IsValid()) return;

        var weapon = pawn.GetActiveWeapon();

        if (round.NoScope && weapon is { IsValidEntity: true })
        {
            var name = weapon.GetWeaponClassname();
            if (!string.IsNullOrEmpty(name) && ScopedWeapons.Contains(name))
                weapon.NextSecondaryAttackTick = _bridge.ModSharp.GetGlobals().TickCount + 128; // ~2s at 64 tick
        }

        if (round.DropOnMiss && pawn.GetController() is { } controller)
        {
            var slot = controller.PlayerSlot.AsPrimitive();
            if (_impactsThisTick[slot] > _hitsThisTick[slot] && weapon is { IsValidEntity: true } && !weapon.IsKnife)
                pawn.DropWeapon(weapon);

            _impactsThisTick[slot] = 0;
            _hitsThisTick[slot]    = 0;
        }
    }
}
