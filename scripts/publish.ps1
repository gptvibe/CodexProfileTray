$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "CodexProfileTray.csproj"
$output = Join-Path $repoRoot "artifacts\publish"
$iconScript = Join-Path $PSScriptRoot "generate-icon.ps1"

& $iconScript

if (Test-Path -LiteralPath $output) {
    Remove-Item -LiteralPath $output -Recurse -Force
}

dotnet publish $project `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:PublishReadyToRun=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    --output $output

Write-Host "Published to $output"
