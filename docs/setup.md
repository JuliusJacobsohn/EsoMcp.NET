# Setup and configuration

[Project overview and quick start](../README.md).

## Requirements

- **ESO for PC/Mac.** Console editions cannot run the required addons.
- The [.NET 10 Runtime or SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
- [Minion](https://minion.mmoui.com/) to install and update the ESO addons below. Install any dependency libraries Minion reports for them as well.

Install and enable these addons for complete account and build data:

| Addon | Data used by EsoMcp.NET |
|---|---|
| [Inventory Insight](https://www.esoui.com/downloads/info731-InventoryInsight.html) | Account-wide inventory locations, including characters, banks, the craft bag and storage |
| [uespLog](https://www.esoui.com/downloads/info1257-uespLog.html) | Character details, skills and skill-line ranks, Champion Points, equipped items, research and statistics |
| [LibCharacterKnowledge](https://www.esoui.com/downloads/info3317-LibCharacterKnowledge.html) | Recipes, furnishing plans, motifs, scribing knowledge and trait research |
| [LibMultiAccountSets](https://www.esoui.com/downloads/info2843-LibMultiAccountSets.html) | Account set-collection progress |
| [Caro's Skill Point Saver](https://www.esoui.com/downloads/info2901-CarosSkillPointSaver.html) | Saved builds, CSPS import/export and current Champion Point definitions |
| [LibSets](https://www.esoui.com/downloads/info2241-LibSets.html) | Current set names and item membership |

These integrations are needed only for their corresponding features:

| Addon | Feature |
|---|---|
| [Dolgubon's Lazy Set Crafter](https://www.esoui.com/downloads/info1697-DolgubonsLazySetCrafter.html) | Read existing crafting queues and apply generated crafting imports in game |
| [Tamriel Trade Centre](https://www.esoui.com/downloads/info1245-TamrielTradeCentre.html) | Local EU/NA price lookup; run the TTC client to keep its price tables current |
| [Loot Log](https://www.esoui.com/downloads/info1455-LootLog.html) | Retained group drops, with recipient account/character and time |
| Combat Metrics | Saved fight summaries: character, time, duration, outgoing/incoming DPS and HPS |

Enable character-data collection in uespLog with `/uesplog on`. Log in to every character that should be represented, visit relevant storage such as the bank, then use `/reloadui` or log out normally so ESO writes the latest observations to disk. Missing providers do not stop the server, but their data and related features will be unavailable.

## Install

1. Install the .NET 10 runtime or SDK.
2. Download and extract `eso-mcp-portable.zip` from [Releases](https://github.com/JuliusJacobsohn/EsoMcp.NET/releases). Keep its files together.
3. Copy `settings.example.json` to `settings.json` and configure source directories if the Windows defaults are unsuitable.
4. Register the server:

```powershell
codex mcp add eso -- dotnet C:/Tools/EsoMcp/eso-mcp.dll --config C:/Tools/EsoMcp/settings.json
```

Other clients use the equivalent stdio command and arguments. Without a configuration file, Windows uses Documents/Elder Scrolls Online/live and `%LOCALAPPDATA%/EsoMcp/data.db`. On other systems, provide local source paths. Save in ESO with `/reloadui` or logout after visiting relevant characters/storage. Files contain the last saved observations, not live game memory.

Existing MCP sessions may retain their old process/tool schemas; reconnect the server or start a new session after upgrading. The server logs to stderr and reserves stdout for MCP messages.

## Configuration

Copy [settings.example.json](../settings.example.json) to `settings.json` and replace every `YOUR_USER` path with your actual directories. Pass the absolute file path with `--config`. Supplying a configuration file replaces automatic source-directory discovery, so configure both `savedVariablesPath` and `addonsPath`. The database is created at `databasePath`.

On Windows, without `--config`, Documents is resolved through the operating system (including redirected Documents folders), and the defaults are:

- SavedVariables: `Documents/Elder Scrolls Online/live/SavedVariables`.
- Addons: `Documents/Elder Scrolls Online/live/AddOns`.
- Database: `%LOCALAPPDATA%/EsoMcp/data.db`.

On macOS or for a different server/install location, provide the actual local paths in the configuration.

CLI options:

```text
--config FILE          JSON configuration
--database FILE        SQLite path
--saved-variables DIR  Source directory override
--addons DIR           Installed addon directory (with --saved-variables)
--server NAME          Default world for otherwise unqualified sources
--database-only        Explicit offline use of persisted account documents
--no-auto-refresh      Explicit offline mode
--refresh              Load/persist accounts and print a compact summary
--status               Inspect persisted account/plan summaries
--help                 Full help
```
