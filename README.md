<div align="center">
  <h1><strong>FunRounds</strong></h1>
  <p>Config-driven special/fun-round engine for CS2 — data rounds in JSON, code rounds via the Shared API</p>
</div>

<p align="center">
  <a href="https://github.com/Kxnrl/modsharp-public"><img src="https://img.shields.io/badge/framework-ModSharp-5865F2?logo=github" alt="ModSharp"></a>
  <img src="https://img.shields.io/badge/game-CS2-orange" alt="CS2">
  <img src="https://img.shields.io/github/stars/yappershq/FunRounds?style=flat&logo=github" alt="Stars">
</p>

---

**FunRounds** is a config-driven fun/special-round engine for CS2. The core module reads round definitions from `configs/funrounds/funrounds.json` at startup — no DLL round-packs needed for data rounds. Damage rules (headshot-only, one-tap), no-scope enforcement, weapon loadouts, weighted random selection, and per-round health are all handled centrally. External packs can also register **code rounds** through `IFunRoundService` delegate callbacks (e.g. a SuperPowers wallhack/gravity round) without requiring a later API change.

Logic ported and rewritten from the CSS gameplay concepts of [laikiux-lt/awp](https://github.com/laikiux-lt/awp) — not a line-for-line port.

## Install

Copy the build output into your ModSharp install (`<sharp>` = your `sharp` directory):

| From | To |
|------|----|
| `.build/modules/FunRounds.Core/` | `<sharp>/modules/FunRounds.Core/` |
| `.build/shared/FunRounds.Shared/` | `<sharp>/shared/FunRounds.Shared/` |
| `.assets/locales/funrounds.json` | `<sharp>/locales/funrounds.json` |

Restart the server (or change map) to load. `configs/funrounds/funrounds.json` is auto-generated on first run with an empty `rounds` array — copy a rounds pack from the examples below to get started.

## Dependencies

Uses the **ModSharp first-party modules** (ship with ModSharp): **CommandCenter** (chat commands), **LocalizerManager** (all user-facing text), **AdminManager** (admin-command permission gating). Each is resolved optionally — commands/text degrade gracefully when a module is absent.

## Commands

Chat triggers: `!`, `/`, or `.`; console prefix `ms_`.

| Command | Description | Permission |
|---------|-------------|------------|
| `!funround <shortName>` | Force a specific fun round this round (by its short name). | `AdminFlag` |
| `!funround_stop` | Stop the active fun round; normal play resumes next round. | `AdminFlag` |
| `!funrounds` | List all registered rounds with their short names. | — (open) |

## Configuration

`configs/funrounds/funrounds.json`:

| Key | Default | Meaning |
|-----|---------|---------|
| `FunRoundChance` | `15` | Percent chance (0–100) that a round becomes a random fun round. `0` = auto off (admins can still force one with `!funround`); `100` = every round. |
| `AnnounceRound` | `true` | Announce the active round: a chat line + a prominent center **win-panel reveal** ("⚡ FUN ROUND ⚡ <name>") when it starts. |
| `AnnounceSeconds` | `5` | How long the win-panel reveal stays on screen before it's hidden (`cs_win_panel_round` shown, then `round_freeze_end` clears it). |
| `AdminFlag` | `"funrounds:manage"` | AdminManager permission flag required to use `!funround` / `!funround_stop`. |
| `Rounds` | `[]` | Array of round definitions — see below. |

### Round config fields

Each entry in `Rounds` supports:

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Name` | string | — | Full display name shown to players (e.g. `"AWP NoScope"`). |
| `ShortName` | string | — | Unique identifier used in `!funround` (e.g. `"awp_ns"`). |
| `PrimaryWeapon` | string\|null | null | CS2 weapon classname (e.g. `"weapon_awp"`). |
| `SecondaryWeapon` | string\|null | null | Secondary weapon classname. |
| `Knife` | bool | false | Give `weapon_knife`. |
| `Taser` | bool | false | Give `weapon_taser`. |
| `HeGrenade` | bool | false | Give `weapon_hegrenade`. |
| `Decoy` | bool | false | Give `weapon_decoy`. |
| `DamageMode` | string | `"Any"` | `"Any"` (normal), `"HeadshotOnly"` (body shots deal 0), or `"OneTap"` (any hit = 9999 damage). |
| `NoScope` | bool | false | Continuously force `m_bIsScoped = false` so scopes are unusable. |
| `Health` | int | 100 | Player health at round start. |
| `Weight` | int | 1 | Relative probability weight for random selection. Higher = appears more often. |

### Example config packs

Three ready-made packs live in `.assets/configs/funrounds/`. Copy the one you want as `configs/funrounds/funrounds.json` on your server (or merge `rounds` arrays from multiple packs into one file):

| File | Rounds |
|------|--------|
| `funrounds.awp.example.json` | AWP NoScope, Scout, Scout HS, Scout NoScope, Scout NoScope HS |
| `funrounds.deagle.example.json` | Deagle, Deagle HeadShot, Deagle One Tap |
| `funrounds.extras.example.json` | Knife Only, AK-47/USP/M4A1-S One Tap, Taser Only, HE + Knife, Decoy War |

Example `funrounds.json` pulling three sniper rounds with custom weights:

```json
{
  "FunRoundChance": 20,
  "AnnounceRound": true,
  "AdminFlag": "funrounds:manage",
  "Rounds": [
    {
      "Name": "AWP NoScope",
      "ShortName": "awp_ns",
      "PrimaryWeapon": "weapon_awp",
      "Knife": true,
      "DamageMode": "Any",
      "NoScope": true,
      "Weight": 2
    },
    {
      "Name": "Deagle",
      "ShortName": "deagle",
      "PrimaryWeapon": "weapon_deagle",
      "Knife": true,
      "Weight": 3
    },
    {
      "Name": "Knife Only",
      "ShortName": "knife",
      "Knife": true,
      "Weight": 1
    }
  ]
}
```

## How it works

`FunRounds.Core` reads `funrounds.json` at startup and registers all `Rounds` entries into `IFunRoundService`. At each round restart `OnRoundRestarted` rolls `FunRoundChance`% — on a hit it picks a **weighted-random** registered round. At `round_poststart` every alive player is stripped and re-armed with the round's loadout. A `PlayerDispatchTraceAttack` pre-hook enforces damage rules: headshot-only rounds zero non-head damage; one-tap rounds set damage to 9999 on any hit. A per-tick `GameFrame` hook continuously forces `m_bIsScoped = false` during no-scope rounds.

## Public API — code/power rounds

External round-pack modules (e.g. `SuperPowers.FunRounds`) can register **code rounds** via the delegate overload on `IFunRoundService`. Resolve the service in `OnAllModulesLoaded`:

```csharp
var svc = sharpModuleManager
    .GetOptionalSharpModuleInterface<IFunRoundService>(IFunRoundService.Identity)?.Instance;

// Data round (no callbacks) — same as putting it in the JSON config:
svc?.Register(new FunRoundBuilder()
    .WithShortName("my_round")
    .WithName("My Round")
    .WithPrimaryWeapon("weapon_ak47")
    .WithHeadshotOnly()
    .WithKnife()
    .WithWeight(2)
    .Build());

// Code round — onApply fires after loadout, onRevert fires at round_end:
svc?.Register(
    new FunRoundBuilder()
        .WithShortName("wallhack")
        .WithName("Wallhack Round")
        .WithPrimaryWeapon("weapon_ak47")
        .WithKnife()
        .Build(),
    onApply: slots =>
    {
        foreach (var slot in slots)
            EnableWallhack(slot);
    },
    onRevert: slots =>
    {
        foreach (var slot in slots)
            DisableWallhack(slot);
    });
```

`FunRoundBuilder` supports: `WithPrimaryWeapon`, `WithSecondaryWeapon`, `WithKnife`, `WithTaser`, `WithHeGrenade`, `WithDecoy`, `WithHeadshotOnly`, `WithOneTap`, `WithNoScope`, `WithHealth`, `WithWeight`.

## Build

```bash
dotnet build -c Release
```

Outputs:
- `.build/modules/FunRounds.Core/FunRounds.dll`
- `.build/shared/FunRounds.Shared/FunRounds.Shared.dll`

## Credits

Round gameplay logic inspired by [laikiux-lt/awp](https://github.com/laikiux-lt/awp) (CSS plugin). Rewritten from scratch to ModSharp conventions.

---

<div align="center">
  <p>Made with love by <a href="https://github.com/yappershq">yappershq</a></p>
</div>
