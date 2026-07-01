using System;
using FunRounds.Shared;
using Microsoft.Extensions.Configuration;
using Sharp.Shared;
using Sharp.Shared.Managers;

namespace FunRounds.Awp;

/// <summary>
/// FunRounds.Awp — the standard round pack.
///
/// Registers 15 round definitions into <see cref="IFunRoundService"/> during
/// OnAllModulesLoaded (guaranteed after FunRounds.Core PostInit, per ModSharp lifecycle).
///
/// Rule: every round gets a knife unless it IS the knife round.
/// </summary>
public sealed class AwpPlugin : IModSharpModule
{
    public string DisplayName   => "FunRounds — Awp Pack";
    public string DisplayAuthor => "yappershq";

    private readonly ISharpModuleManager _sharpModuleManager;

    public AwpPlugin(
        ISharedSystem   sharedSystem,
        string?         dllPath,
        string?         sharpPath,
        Version?        version,
        IConfiguration? coreConfiguration,
        bool            hotReload)
    {
        ArgumentNullException.ThrowIfNull(sharedSystem);
        _sharpModuleManager = sharedSystem.GetSharpModuleManager();
    }

    public bool Init()            => true;
    public void PostInit()        { }
    public void Shutdown()        { }

    public void OnAllModulesLoaded()
    {
        var iface = _sharpModuleManager
            .GetOptionalSharpModuleInterface<IFunRoundService>(IFunRoundService.Identity);

        if (iface?.Instance is not { } svc)
            return; // FunRounds.Core not loaded — silently skip

        // Helper: builder pre-wired with a knife (standard for this pack)
        static FunRoundBuilder B(string shortName, string name)
            => new FunRoundBuilder().WithShortName(shortName).WithName(name).WithKnife();

        // ── 15 rounds ──────────────────────────────────────────────────────

        // 1. AWP NoScope
        svc.Register(B("awp_ns", "AWP NoScope")
            .WithPrimaryWeapon("weapon_awp").WithNoScope()
            .Build());

        // 2. Deagle
        svc.Register(B("deagle", "Deagle")
            .WithPrimaryWeapon("weapon_deagle")
            .Build());

        // 3. Scout
        svc.Register(B("scout", "Scout")
            .WithPrimaryWeapon("weapon_ssg08")
            .Build());

        // 4. Knife Only — knife IS the weapon; no additional knife needed
        svc.Register(new FunRoundBuilder()
            .WithShortName("knife").WithName("Knife Only")
            .WithKnife()
            .Build());

        // 5. Deagle HeadShot
        svc.Register(B("deagle_hs", "Deagle HeadShot")
            .WithPrimaryWeapon("weapon_deagle").WithHeadshotOnly()
            .Build());

        // 6. Scout HeadShot
        svc.Register(B("scout_hs", "Scout HeadShot")
            .WithPrimaryWeapon("weapon_ssg08").WithHeadshotOnly()
            .Build());

        // 7. Scout NoScope
        svc.Register(B("scout_ns", "Scout NoScope")
            .WithPrimaryWeapon("weapon_ssg08").WithNoScope()
            .Build());

        // 8. Scout NoScope HeadShot
        svc.Register(B("scout_ns_hs", "Scout NoScope HeadShot")
            .WithPrimaryWeapon("weapon_ssg08").WithNoScope().WithHeadshotOnly()
            .Build());

        // 9. AK-47 One Tap
        svc.Register(B("ak47_ot", "AK-47 One Tap")
            .WithPrimaryWeapon("weapon_ak47").WithOneTap()
            .Build());

        // 10. USP One Tap
        svc.Register(B("usp_ot", "USP One Tap")
            .WithPrimaryWeapon("weapon_usp_silencer").WithOneTap()
            .Build());

        // 11. Deagle One Tap
        svc.Register(B("deagle_ot", "Deagle One Tap")
            .WithPrimaryWeapon("weapon_deagle").WithOneTap()
            .Build());

        // 12. M4A1-S One Tap
        svc.Register(B("m4a1_ot", "M4A1-S One Tap")
            .WithPrimaryWeapon("weapon_m4a1_silencer").WithOneTap()
            .Build());

        // 13. Taser Only
        svc.Register(B("taser", "Taser Only")
            .WithTaser()
            .Build());

        // 14. HE + Knife
        svc.Register(new FunRoundBuilder()
            .WithShortName("henade").WithName("HE + Knife")
            .WithHeGrenade().WithKnife()
            .Build());

        // 15. Decoy War
        svc.Register(new FunRoundBuilder()
            .WithShortName("decoy").WithName("Decoy War")
            .WithDecoy().WithKnife()
            .Build());
    }
}
