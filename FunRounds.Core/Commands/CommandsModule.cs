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

        reg.RegisterClientCommand("funround",      OnFunRound);
        reg.RegisterClientCommand("funround_stop", OnFunRoundStop);
        reg.RegisterClientCommand("funrounds",     OnFunRounds);
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
        if (!_service.StartRound(shortName))
        {
            Loc.Chat(_bridge.LocalizerManager, client, "FunRounds_UnknownRound", shortName);
            return;
        }

        var round = _service.Current!;
        Loc.ChatAll(_bridge.LocalizerManager, _bridge.ClientManager,
            "FunRounds_RoundSelected", round.Name);
        _logger.LogInformation("[FunRounds] {Admin} started round '{Round}'.",
            client.Name, round.Name);
    }

    // ── !funround_stop ────────────────────────────────────────────────────

    private void OnFunRoundStop(IGameClient client, StringCommand cmd)
    {
        if (Denied(client)) return;

        _service.StopRound();
        Loc.Chat(_bridge.LocalizerManager, client, "FunRounds_Stopped");
        _logger.LogInformation("[FunRounds] {Admin} stopped the fun round.", client.Name);
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
