# EsoMcp.NET

[![CI](https://github.com/JuliusJacobsohn/EsoMcp.NET/actions/workflows/ci.yml/badge.svg)](https://github.com/JuliusJacobsohn/EsoMcp.NET/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/JuliusJacobsohn/EsoMcp.NET)](https://github.com/JuliusJacobsohn/EsoMcp.NET/releases)

A local Model Context Protocol (MCP) server that lets AI clients work with [The Elder Scrolls: Online](https://www.elderscrollsonline.com/) addon data. Built on [EsoData.NET](https://github.com/JuliusJacobsohn/EsoData.NET), it provides tools to inspect accounts, plan character builds and generate imports for use in game.

> [!WARNING]
> Created with substantial help from **GPT-6.1 Sol** and tested against my own use cases. Please do not treat this project as a measure of my abilities as a developer, for better or worse.

## Features

- Inspect characters, skills, Champion Points, crafting knowledge and set collections.
- Search inventory across characters, banks, craft bags and other saved storage.
- Maintain named build drafts, compare them with your account, and check prerequisites.
- Generate CSPS and crafting imports, then compare the result with newly saved game state.
- Query retained group loot, saved combat summaries and local TTC market prices.

The server reads local addon files and stores accounts and plans in SQLite. It never modifies game files. Catalog downloads request game definitions without sending account data; information returned to your AI client is handled by that client.

## Installation

Requires **ESO for PC/Mac**, the **.NET 10 runtime or SDK**, an MCP client, and the addons needed for your chosen features. The [setup guide](docs/setup.md#requirements) lists the addons and how to enable their data collection.

1. Download `eso-mcp-portable.zip` from [Releases](https://github.com/JuliusJacobsohn/EsoMcp.NET/releases).
2. Extract it to a folder such as `C:/Tools/EsoMcp`, keeping the files together.
3. Register `dotnet` as the stdio command and the absolute path to `eso-mcp.dll` as its argument in your MCP client.

For example, with Codex CLI on Windows:

```powershell
codex mcp add eso -- dotnet C:/Tools/EsoMcp/eso-mcp.dll
```

On Windows, no configuration file is needed for the usual ESO installation. The server finds `Documents/Elder Scrolls Online/live/SavedVariables` and the adjacent `AddOns` directory through your Windows Documents folder, including redirected folders. Its database defaults to `%LOCALAPPDATA%/EsoMcp/data.db`.

For other locations or operating systems, copy the included `settings.example.json`, replace its example paths, and pass it with `--config C:/Tools/EsoMcp/settings.json`. See [configuration](docs/setup.md#configuration). Reconnect your MCP client after upgrading the server.

## Quick start

Install and enable **Inventory Insight (IIfA)** for inventory queries. For character skills and builds, enable **uespLog** and its character-data collection with `/uesplog on`. Add further providers as described in the [setup guide](docs/setup.md#requirements).

Log in to the characters you want to inspect, open the bank and relevant storage, then use `/reloadui` or log out normally. Start or reconnect your MCP client and ask:

> Show my ESO accounts and characters, then list the inventory for the account I choose.

The client can discover accounts through `inspect_account` without arguments. Once the saved data is available, try:

> Compare a proposed build with my character's saved setup and the equipment available across my account.

Sources reflect saved addon observations. Visit the relevant characters and storage, then save again when you need fresh data. Build drafts remain separate from the setup observed in game; apply generated imports yourself and save before asking the client to verify them.

## Supported integrations

| Integration | Purpose |
|---|---|
| IIfA / uespLog | Inventory and character state |
| LibCharacterKnowledge / LibMultiAccountSets | Crafting knowledge, research and set collections |
| Caro's Skill Point Saver / ESO-Hub | Saved builds and build formats |
| LibSets / UESP / external catalogs | Game definitions and lookups |
| Lazy Set Crafter / LibLazyCrafting | Crafting imports and queues |
| Loot Log / Combat Metrics | Retained drops and saved fight summaries |
| Tamriel Trade Centre | Prices from locally downloaded market data |

Missing addons leave their corresponding data unavailable. The server can still use the sources that are present.

## Tools

| Tool | Purpose |
|---|---|
| `inspect_account` | Discover accounts and query saved character and account data |
| `resolve_definitions` | Look up items, sets, skills and Champion Points |
| `edit_build` | Create and edit named build drafts |
| `analyze_build` | Compare builds and check requirements, equipment and crafting needs |
| `export_build` | Generate selected CSPS sections or crafting imports |
| `verify_build` | Compare a build revision with newly saved game state |
| `query_prices` | Search the local TTC market catalog |
| `refresh_catalog` | Download selected game definitions from UESP |

## Documentation

- [Setup and configuration](docs/setup.md): addons, source directories, configuration and CLI options.
- [Tool usage guide](docs/usage.md): account queries, build workflows, loot, prices, crafting and data coverage.
- [Architecture](docs/account-architecture.md): accounts, observations, plans and persistence.
- [Development and releases](docs/development.md): local checks, protocol smoke testing and publishing.
- [EsoData.NET](https://github.com/JuliusJacobsohn/EsoData.NET): the underlying library.

## Development

```powershell
pwsh -File scripts/Restore-EsoData.ps1
dotnet test EsoMcp.sln -c Release
dotnet run --project samples/EsoMcp.Smoke -c Release
```

The default tests and smoke check use synthetic fixtures. CI runs on Windows and Linux.

Maintained by Julius Jacobsohn. Not affiliated with or endorsed by ZeniMax Media or Bethesda.
