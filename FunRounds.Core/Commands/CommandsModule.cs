using System.Collections.Generic;
using System.Linq;
using System.Text;
using FunRounds.Config;
using FunRounds.Plugins;
using FunRounds.Rounds;
using FunRounds.Utils;
using Microsoft.Extensions.Logging;
using Sharp.Modules.AdminManager.Shared;
using Sharp.Modules.CommandCenter.Shared;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using Sharp.Shared.Units;

namespace FunRounds.Commands;

/// <summary>
/// Registers player/admin commands via CommandCenter (bare names, no css_ prefix):
///   funround &lt;shortName&gt; — admin-only: force a specific fun round
///   funround_stop         — admin-only: stop the active fun round
///   funrounds             — any player: list all registered rounds
/// </summary>
internal sealed class CommandsModule : IModule
{
    private readonly ILogger<CommandsModule> _logger;
    private readonly InterfaceBridge         _bridge;
    private readonly ConfigModule            _config;
    private readonly FunRoundService         _service;

    private IAdminManager? _adminManager;

    public CommandsModule(
        ILogger<CommandsModule> logger,
        InterfaceBridge         bridge,
        ConfigModule            config,
        FunRoundService         service)
    {
        _logger  = logger;
        _bridge  = bridge;
        _config  = config;
        _service = service;
    }

    // ── IModule lifecycle ──────────────────────────────────────────────────

    public bool Init() => true;

    public void OnPostInit() { }

    public void OnAllSharpModulesLoaded()
    {
        _adminManager = _bridge.SharpModuleManager
            .GetOptionalSharpModuleInterface<IAdminManager>(IAdminManager.Identity)?.Instance;

        if (_adminManager is not null)
        {
            // Register the custom permission so wildcard admins ("*") resolve it correctly.
            var flag = _config.Config.AdminFlag;
            _adminManager.MountAdminManifest("FunRounds", () => new AdminTableManifest(
                PermissionCollection: new Dictionary<string, System.Collections.Generic.HashSet<string>>
                {
                    [flag] = [],
                },
                Roles:  [],
                Admins: []
            ));
        }
        else
        {
            _logger.LogWarning("[FunRounds] AdminManager not available — admin commands will be denied.");
        }

        var cc = _bridge.SharpModuleManager
            .GetOptionalSharpModuleInterface<ICommandCenter>(ICommandCenter.Identity)?.Instance;

        if (cc is null)
        {
            _logger.LogWarning("[FunRounds] CommandCenter not available — commands disabled.");
            return;
        }

        var reg = cc.GetRegistry("funrounds");

        // Client chat commands (!funround / client console ms_funround).
        reg.RegisterClientCommand("funround",      OnFunRound);
        reg.RegisterClientCommand("funround_stop", OnFunRoundStop);
        reg.RegisterClientCommand("funrounds",     OnFunRounds);

        // Server console / RCON commands (ms_funround … — trusted, no admin gate).
        reg.RegisterServerCommand("funround",      OnFunRoundServer, "Force a fun round next round: funround <shortName>");
        reg.RegisterServerCommand("funround_stop", OnFunRoundStopServer, "Cancel the active/queued fun round");
        reg.RegisterServerCommand("funrounds",     OnFunRoundsServer, "List registered fun rounds");
    }

    public void Shutdown() { }

    // ── Admin gate ────────────────────────────────────────────────────────

    private bool Denied(IGameClient client)
    {
        var flag  = _config.Config.AdminFlag;
        var steam = (SteamID)client.SteamId;
        if (_adminManager?.GetAdmin(steam)?.HasPermission(flag) == true)
            return false;

        Loc.Chat(_bridge.LocalizerManager, client, "FunRounds_NoPermission");
        return true;
    }

    // ── !funround <shortName> ─────────────────────────────────────────────

    private void OnFunRound(IGameClient client, StringCommand cmd)
    {
        if (Denied(client)) return;

        if (cmd.ArgCount < 1)
        {
            Loc.Chat(_bridge.LocalizerManager, client, "FunRounds_Usage_FunRound");
            return;
        }

        var shortName = cmd.GetArg(1);
        // Queue for NEXT round — a fun round applies its loadout at round_poststart, so it can't
        // take effect mid-round; forcing always targets the next round.
        if (!_service.QueueForced(shortName))
        {
            Loc.Chat(_bridge.LocalizerManager, client, "FunRounds_UnknownRound", shortName);
            return;
        }

        Loc.Chat(_bridge.LocalizerManager, client, "FunRounds_RoundQueued", shortName);
        _logger.LogInformation("[FunRounds] {Admin} queued round '{Round}' for next round.",
            client.Name, shortName);
    }

    // ── !funround_stop ────────────────────────────────────────────────────

    private void OnFunRoundStop(IGameClient client, StringCommand cmd)
    {
        if (Denied(client)) return;

        _service.DequeueForced();
        _service.StopRound();
        Loc.Chat(_bridge.LocalizerManager, client, "FunRounds_Stopped");
        _logger.LogInformation("[FunRounds] {Admin} stopped the fun round.", client.Name);
    }

    // ── server console / RCON handlers (trusted context, no admin gate) ────

    private void OnFunRoundServer(StringCommand cmd)
    {
        if (cmd.ArgCount < 1)
        {
            _logger.LogInformation("[FunRounds] Usage: funround <shortName>");
            return;
        }

        var shortName = cmd.GetArg(1);
        if (!_service.QueueForced(shortName))
        {
            _logger.LogWarning("[FunRounds] Unknown round '{Short}'.", shortName);
            return;
        }

        _logger.LogInformation("[FunRounds] Console queued round '{Short}' for next round.", shortName);
    }

    private void OnFunRoundStopServer(StringCommand cmd)
    {
        _service.DequeueForced();
        _service.StopRound();
        _logger.LogInformation("[FunRounds] Console cancelled the fun round.");
    }

    private void OnFunRoundsServer(StringCommand cmd)
    {
        var rounds = _service.Registered;
        _logger.LogInformation("[FunRounds] {Count} registered round(s): {List}",
            rounds.Count, string.Join(", ", rounds.Select(r => r.ShortName)));
    }

    // ── !funrounds ────────────────────────────────────────────────────────

    private void OnFunRounds(IGameClient client, StringCommand cmd)
    {
        var rounds = _service.Registered;
        if (rounds.Count == 0)
        {
            Loc.Chat(_bridge.LocalizerManager, client, "FunRounds_NoRoundsRegistered");
            return;
        }

        Loc.Chat(_bridge.LocalizerManager, client, "FunRounds_List_Header", rounds.Count);
        foreach (var r in rounds)
            Loc.Chat(_bridge.LocalizerManager, client, "FunRounds_List_Entry", r.ShortName, r.Name);
    }
}
