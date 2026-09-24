$project = [xml](Get-Content (Join-Path $PSScriptRoot '../src/EsoMcp.Import/EsoMcp.Import.csproj') -Raw)
$reference = $project.Project.ItemGroup.PackageReference | Where-Object Include -eq 'EsoData.NET'
$version = $reference.Version
if (-not $version) { throw 'EsoData.NET PackageReference version is missing.' }

$feed = Join-Path $PSScriptRoot '../artifacts/nuget'
New-Item -ItemType Directory -Force -Path $feed | Out-Null
gh release download "v$version" --repo JuliusJacobsohn/EsoData.NET `
    --pattern "EsoData.NET.$version.nupkg" --dir $feed --clobber
if ($LASTEXITCODE -ne 0) { throw "Could not download EsoData.NET $version from its release." }

dotnet restore (Join-Path $PSScriptRoot '../EsoMcp.sln') `
    --source $feed --source 'https://api.nuget.org/v3/index.json'
if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }
