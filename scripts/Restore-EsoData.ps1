$project = [xml](Get-Content (Join-Path $PSScriptRoot '../src/EsoMcp.Core/EsoMcp.Core.csproj') -Raw)
$reference = $project.Project.ItemGroup.PackageReference | Where-Object Include -eq 'EsoData.NET'
$version = $reference.Version
if (-not $version) { throw 'EsoData.NET PackageReference version is missing.' }

$feed = Join-Path $PSScriptRoot '../artifacts/nuget'
New-Item -ItemType Directory -Force -Path $feed | Out-Null
gh release download "v$version" --repo JuliusJacobsohn/EsoData.NET `
    --pattern "EsoData.NET.$version.nupkg" --dir $feed --clobber
if ($LASTEXITCODE -ne 0) { throw "Could not download EsoData.NET $version from its release." }

$configPath = Join-Path $feed 'NuGet.Config'
$feedUri = [System.Security.SecurityElement]::Escape((Resolve-Path $feed).Path)
@"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="release" value="$feedUri" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
"@ | Set-Content -LiteralPath $configPath -Encoding utf8

dotnet restore (Join-Path $PSScriptRoot '../EsoMcp.sln') --configfile $configPath
if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }
