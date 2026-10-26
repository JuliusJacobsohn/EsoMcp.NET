# Development and releases

## Development and verification

```powershell
pwsh -File scripts/Restore-EsoData.ps1
dotnet test EsoMcp.sln -c Release
dotnet run --project samples/EsoMcp.Smoke -c Release
```

The smoke program uses the official .NET MCP client against the real stdio server. It verifies discovery, account reads, draft mutation, stale-write rejection, export, comparison and persistence across server restarts. Its default fixture is synthetic. Add `-- <server.dll> --live` to exercise local game data; it creates and deletes only its own temporary verification draft and never changes game files. `--request FILE` executes one explicit JSON `{tool,arguments}` request for integration diagnostics.

For simultaneous library/server development with sibling repositories, `-p:UseLocalEsoData=true` references the local library. Releases and CI use the published NuGet package. Windows and Ubuntu CI run tests and the .NET protocol smoke check. Dependency restore uses NuGet first and falls back to the library's GitHub release asset if the package is not yet indexed. Publish the library before updating the MCP dependency so a fresh CI runner can restore it.

## Releases

Update `Directory.Build.props` with a new server version and use the intended published EsoData.NET version in `src/EsoMcp.Core/EsoMcp.Core.csproj`. Commit and push, wait for CI to pass, then create and push a matching `v<version>` tag. The Release workflow builds, tests, runs the protocol smoke check and publishes `eso-mcp-portable.zip` on GitHub. The archive includes the README, setup and usage documentation, and example configuration at the top level. The framework-dependent DLL and its required runtime files are under `server/`; PDB debugging symbols are excluded. Launch it with `dotnet server/eso-mcp.dll`.

Never move a published tag; create a new version for a new release.
