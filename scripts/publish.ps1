$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root "Release"
$portable = Join-Path $root "IPFamilySwitcher-Portable-x64.zip"

if (Test-Path $output) {
    Remove-Item $output -Recurse -Force
}

dotnet publish (Join-Path $root "IPFamilySwitcher\IPFamilySwitcher.csproj") `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $output

if (Test-Path $portable) {
    Remove-Item $portable -Force
}

Compress-Archive -Path (Join-Path $output "*") -DestinationPath $portable
Write-Host "Published: $output"
Write-Host "Portable archive: $portable"
