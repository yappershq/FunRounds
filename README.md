<div align="center">
  <h1><strong>FunRounds</strong></h1>
  <p>Pluggable special/fun-round engine for CS2 — round packs register through a shared API</p>
</div>

<p align="center">
  <a href="https://github.com/Kxnrl/modsharp-public"><img src="https://img.shields.io/badge/framework-ModSharp-5865F2?logo=github" alt="ModSharp"></a>
  <img src="https://img.shields.io/badge/game-CS2-orange" alt="CS2">
  <img src="https://img.shields.io/github/stars/yappershq/FunRounds?style=flat&logo=github" alt="Stars">
</p>

---

**FunRounds** is a pluggable fun/special-round engine for CS2. The core module publishes `IFunRoundService` — external round-pack modules register their rounds through this API and the engine applies them each round (auto-random or on admin command). Damage rules (headshot-only, one-tap), no-scope enforcement, and weapon loadouts are handled centrally. A bundled **Awp Pack** ships 15 rounds out of the box; adding more is one plugin file.

Logic ported and rewritten from the CSS gameplay concepts of [laikiux-lt/awp](https://github.com/laikiux-lt/awp) — not a line-for-line port.

## 🚀 Install

Copy the build output into your ModSharp install (`<sharp>` = your `sharp` directory):

**Core (required):**

| From | To |
|------|----|
| `.build/modules/FunRounds.Core/` | `<sharp>/modules/FunRounds.Core/` |
| `.build/shared/FunRounds.Shared/` | `<sharp>/shared/FunRounds.Shared/` |
| `.assets/locales/funrounds.json` | `<sharp>/locales/funrounds.json` |

**Awp Pack (optional, bundled):**

| From | To |
|------|----|
| `.build/modules/FunRounds.Awp/` | `<sharp>/modules/FunRounds.Awp/` |

Restart the server (or change map) to load. `configs/funrounds/funrounds.json` is auto-generated on first run.

## 🧩 Dependencies

Uses the **ModSharp first-party modules** (ship with ModSharp): **CommandCenter** (chat commands), **LocalizerManager** (all user-facing text), **AdminManager** (admin-command permission gating). Each is resolved optionally — commands/text degrade gracefully when a module is absent.

## ⌨️ Commands

Chat triggers: `!`, `/`, or `.`; console prefix `ms_`.

| Command | Description | Permission |
|---------|-------------|------------|
| `!funround <shortName>` | Force a specific fun round this round (by its short name). | `AdminFlag` |
| `!funround_stop` | Stop the active fun round; normal play resumes next round. | `AdminFlag` |
| `!funrounds` | List all registered rounds with their short names. | — (open) |

## ⚙️ Configuration

`configs/funrounds/funrounds.json` (auto-generated on first run):

| Key | Default | Meaning |
|-----|---------|---------|
| `AutoRandomRound` | `false` | Pick a random registered round automatically each round. When `false`, rounds start only via `!funround`. |
| `AnnounceRound` | `true` | Broadcast the active round name to all players in chat when it starts. |
| `AdminFlag` | `"funrounds:manage"` | AdminManager permission flag required to use `!funround` / `!funround_stop`. Accepts any string recognized by AdminManager (e.g. `"@css/generic"`). |

## 🔧 How it works

`FunRounds.Core` hooks `round_poststart` to strip every alive player's weapons and give the current round's loadout. A `PlayerDispatchTraceAttack` pre-hook enforces damage rules: headshot-only rounds zero non-head damage; one-tap rounds set damage to 9999 on any hit. A per-tick `GameFrame` hook continuously forces `m_bIsScoped = false` during no-scope rounds. When `AutoRandomRound` is on, `OnRoundRestarted` (fired at round-restart) picks a random registered round before `round_poststart` fires.

Round packs are separate modules — they resolve `IFunRoundService` in their `OnAllModulesLoaded` (after Core's `PostInit` publishes it) and call `Register()` for each round they provide.

## 🎯 Awp Pack rounds

The bundled `FunRounds.Awp` module ships 15 round definitions:

| Short name | Display name |
|------------|--------------|
| `awp_ns` | AWP NoScope |
| `deagle` | Deagle |
| `scout` | Scout |
| `knife` | Knife Only |
| `deagle_hs` | Deagle HeadShot |
| `scout_hs` | Scout HeadShot |
| `scout_ns` | Scout NoScope |
| `scout_ns_hs` | Scout NoScope HeadShot |
| `ak47_ot` | AK-47 One Tap |
| `usp_ot` | USP One Tap |
| `deagle_ot` | Deagle One Tap |
| `m4a1_ot` | M4A1-S One Tap |
| `taser` | Taser Only |
| `henade` | HE + Knife |
| `decoy` | Decoy War |

## 📡 Public API

External round-pack modules consume `IFunRoundService` (resolve in `OnAllModulesLoaded`):

```csharp
var svc = sharpModuleManager
    .GetOptionalSharpModuleInterface<IFunRoundService>(IFunRoundService.Identity)?.Instance;

svc?.Register(new FunRoundBuilder()
    .WithShortName("my_round")
    .WithName("My Round")
    .WithPrimaryWeapon("weapon_ak47")
    .WithHeadshotOnly()
    .WithKnife()
    .Build());
```

`FunRoundBuilder` supports: `WithPrimaryWeapon`, `WithSecondaryWeapon`, `WithKnife`, `WithTaser`, `WithHeGrenade`, `WithDecoy`, `WithHeadshotOnly`, `WithOneTap`, `WithNoScope`, `WithHealth`.

`FunRounds.Awp` is a reference consumer — see its source for a complete example.

## 📦 Build

```bash
dotnet build -c Release
```

Outputs:
- `.build/modules/FunRounds.Core/FunRounds.dll`
- `.build/modules/FunRounds.Awp/FunRounds.Awp.dll`
- `.build/shared/FunRounds.Shared/FunRounds.Shared.dll`

## 🙏 Credits

Round gameplay logic inspired by [laikiux-lt/awp](https://github.com/laikiux-lt/awp) (CSS plugin). Rewritten from scratch to ModSharp conventions.

---

<div align="center">
  <p>Made with ❤️ by <a href="https://github.com/yappershq">yappershq</a></p>
  <p>⭐ Star this repo if you find it useful!</p>
</div>
