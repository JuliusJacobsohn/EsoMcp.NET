![EsoMcp.NET logo](https://raw.githubusercontent.com/JuliusJacobsohn/EsoMcp.NET/main/assets/logo.svg)

# EsoMcp.NET

[![CI](https://github.com/JuliusJacobsohn/EsoMcp.NET/actions/workflows/ci.yml/badge.svg)](https://github.com/JuliusJacobsohn/EsoMcp.NET/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/JuliusJacobsohn/EsoMcp.NET)](https://github.com/JuliusJacobsohn/EsoMcp.NET/releases/latest)

Let your AI assistant help with your characters, gear and builds in [The Elder Scrolls: Online](https://www.elderscrollsonline.com/). EsoMcp.NET connects your saved addon data to an AI client through the Model Context Protocol (MCP), using [EsoData.NET](https://github.com/JuliusJacobsohn/EsoData.NET).

**[Download the latest release](https://github.com/JuliusJacobsohn/EsoMcp.NET/releases/latest) · [Quick install](#quick-install)**

> [!WARNING]
> Created with substantial help from **GPT-6.1 Sol** and tested against my own use cases. Please do not treat this project as a measure of my abilities as a developer, for better or worse.

## What can it help with?

Ask your assistant things like:

- “I found this cool [Hyperioxes Dragonknight tank build](https://hyperioxes.com/eso/tank/dragonknight-build). Give me a to-do list of everything I'm missing to complete it.”
- “Check whether my ESO addons are installed and where their saved data is.”
- “Find this set across all my characters and the bank.”
- “Compare this build with my character. Which gear and skills am I missing?”
- “Which character knows this recipe or crafting trait?”
- “Make a CSPS import for this setup using the equipment I already own.”
- “Show recent group drops I haven't collected, and draft a loot request.”
- “Which of my item stacks are worth the most according to my downloaded TTC prices?”

Available answers depend on the addons and game definitions you have. Account data and build drafts are stored locally; your AI client receives the information it requests.

## What can't it do?

- **Play the game for you.** It cannot move your character, fight, run dungeons or operate the game UI.
- **Apply builds or craft items for you.** It generates supported imports and commands; you apply them in ESO yourself.
- **See live game state.** It reads what your addons last saved. Visit characters and storage, then use `/reloadui` or log out to update that data.
- **Know information your addons haven't recorded.** Missing scans and definitions can leave questions unanswered.

## Quick install

You need ESO on **PC/Mac** and an AI client that supports local MCP servers. The **Windows standalone executable includes .NET**, so you do not need to install a runtime separately.

1. Install **Inventory Insight** for inventory and **uespLog** for character/build data. The [addon list](#addons) links to these and the optional integrations. [Minion](https://minion.mmoui.com/) can help manage them; install their dependency libraries too.
2. Enable the addons in ESO. Enable uespLog's character collection with `/uesplog on`. Log in to the characters you want to inspect, open the bank and relevant storage, then use `/reloadui` or log out normally.
3. On Windows x64, download **`eso-mcp-win-x64.zip`** from the [latest release](https://github.com/JuliusJacobsohn/EsoMcp.NET/releases/latest) and extract it, for example to `C:/Tools/EsoMcp`.
4. Add it to your AI client's MCP settings: command **`C:/Tools/EsoMcp/eso-mcp.exe`**, with no arguments required. Use your actual extraction path.

For Codex CLI, step 4 is:

```powershell
codex mcp add eso -- C:/Tools/EsoMcp/eso-mcp.exe
```

On Windows, the usual ESO folders are found automatically under `Documents/Elder Scrolls Online/live`: `SavedVariables` for saved data and `AddOns` for installed addons. This also follows redirected Windows Documents folders. The database is stored at `%LOCALAPPDATA%/EsoMcp/data.db`.

For macOS/Linux, use **`eso-mcp-portable.zip`** and the [.NET 10 runtime](https://dotnet.microsoft.com/download/dotnet/10.0). Register command `dotnet` with the absolute path to `server/eso-mcp.dll` as its argument.

For another installation location or a non-Windows system, copy the included `settings.example.json`, replace its example paths, and add `--config` followed by the absolute path to your settings file. See the [setup guide](docs/setup.md#configuration) for details. Reconnect your AI client after installing or upgrading, then try:

> Check my ESO addon installation, then show my accounts and characters.

## Addons

Start with the addons for the features you want. You do not need to install every optional integration.

| Addon | What it makes available |
|---|---|
| [Inventory Insight (IIfA)](https://www.esoui.com/downloads/info731-InventoryInsight.html) | Items across characters, banks, craft bags and storage |
| [uespLog](https://www.esoui.com/downloads/info1257-uespLog.html) | Character details, skills, Champion Points, gear and research |
| [LibCharacterKnowledge](https://www.esoui.com/downloads/info3317-LibCharacterKnowledge.html) | Known recipes, plans, motifs, scribing and crafting research |
| [LibMultiAccountSets](https://www.esoui.com/downloads/info2843-LibMultiAccountSets.html) | Collected set pieces |
| [Caro's Skill Point Saver](https://www.esoui.com/downloads/info2901-CarosSkillPointSaver.html) | Saved builds and applying generated build imports |
| [LibSets](https://www.esoui.com/downloads/info2241-LibSets.html) | Set names and item membership |
| [Dolgubon's Lazy Set Crafter](https://www.esoui.com/downloads/info1697-DolgubonsLazySetCrafter.html) | Equipment crafting queues and imports |
| [LibLazyCrafting](https://github.com/Dolgubon/LibLazyCrafting) | Supported glyph and provisioning crafting queues |
| [Tamriel Trade Centre](https://www.esoui.com/downloads/info1245-TamrielTradeCentre.html) | Market prices from tables downloaded by the TTC client |
| [Loot Log](https://www.esoui.com/downloads/info1455-LootLog.html) | Retained group drops and loot-request drafts |
| [Combat Metrics](https://www.esoui.com/downloads/info1360-CombatMetrics.html) | Fights you have explicitly saved in its report window |

## Further reading

- [Setup and troubleshooting](docs/setup.md): addons, paths, configuration and command-line options.
- [Tool usage guide](docs/usage.md): tool reference and detailed build, loot, price and crafting workflows.
- [Development and releases](docs/development.md): building, testing and publishing the server.
- [Architecture](docs/account-architecture.md) and the [EsoData.NET library](https://github.com/JuliusJacobsohn/EsoData.NET).

Maintained by Julius Jacobsohn. Not affiliated with or endorsed by ZeniMax Media or Bethesda.
