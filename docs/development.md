# Development and releases

## Development and verification

```powershell
pwsh -File scripts/Restore-EsoData.ps1
dotnet test EsoMcp.sln -c Release
dotnet run --project samples/EsoMcp.Smoke -c Release
```

The smoke program uses the official .NET MCP client against the real stdio server. It verifies discovery, account reads, draft mutation, stale-write rejection, export, comparison and persistence across server restarts. Its default fixture is synthetic. Pass a DLL or executable path after `--` to test that published artifact; DLLs run through `dotnet` and executables launch directly. Add `--live` to exercise local game data; it creates and deletes only its own temporary verification draft and never changes game files. `--request FILE` executes one explicit JSON `{tool,arguments}` request for integration diagnostics.

To build and verify the Windows standalone executable locally:

```powershell
dotnet publish src/EsoMcp.Server -c Release -p:PublishProfile=Standalone -o artifacts/standalone
dotnet run --project samples/EsoMcp.Smoke -c Release -- artifacts/standalone/eso-mcp.exe
```

The publish profile targets Windows x64, includes .NET and native libraries, and disables trimming and loose debug symbols. Windows CI checks that the publish output contains exactly one file and runs the protocol smoke check against that executable.

For simultaneous library/server development with sibling repositories, `-p:UseLocalEsoData=true` references the local library. Releases and CI use the published NuGet package. Windows and Ubuntu CI run tests and the .NET protocol smoke check. Dependency restore uses NuGet first and falls back to the library's GitHub release asset if the package is not yet indexed. Publish the library before updating the MCP dependency so a fresh CI runner can restore it.

## Releases

Update `Directory.Build.props` with a new server version and use the intended published EsoData.NET version in `src/EsoMcp.Core/EsoMcp.Core.csproj`. Commit and push, wait for CI to pass, then create and push a matching `v<version>` tag. The Release workflow builds, tests, runs the protocol smoke check and publishes two archives on GitHub:

- `eso-mcp-win-x64.zip` contains the standalone `eso-mcp.exe`, with no loose runtime DLLs or PDBs. The executable includes the .NET runtime and extracts bundled native libraries when needed.
- `eso-mcp-portable.zip` contains the framework-dependent DLL and its required runtime files under `server/`. Launch it with `dotnet server/eso-mcp.dll`.

Both archives include the README, setup and usage documentation, and example configuration at the top level.

Never move a published tag; create a new version for a new release.
