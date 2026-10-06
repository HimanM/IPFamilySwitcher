param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$release = Get-Content -LiteralPath (Join-Path $root 'version.json') -Raw | ConvertFrom-Json
if ($release.version -notmatch '^\d+\.\d+\.\d+$' -or !$release.releaseNotes.Count) { throw 'version.json requires a numeric major.minor.patch version and releaseNotes.' }
if (!$OutputDirectory) { $OutputDirectory = Join-Path $root ('Release/v' + $release.version) }
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $output | Out-Null
dotnet publish (Join-Path $root 'IPFamilySwitcher/IPFamilySwitcher.csproj') --configuration Release --runtime win-x64 --self-contained true --output $output "-p:Version=$($release.version)" "-p:InformationalVersion=$($release.version)"
if ($LASTEXITCODE -ne 0) { throw 'Publish failed. Close any running executable in the output directory and retry.' }
Copy-Item -LiteralPath (Join-Path $root 'version.json') -Destination $output
Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination $output
$portable = Join-Path $root "IPFamilySwitcher-Portable-x64-v$($release.version).zip"
Compress-Archive -Path (Join-Path $output '*') -DestinationPath $portable -Force
$hash = (Get-FileHash -LiteralPath $portable -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $([IO.Path]::GetFileName($portable))" | Set-Content -LiteralPath "$portable.sha256" -Encoding utf8
$release.releaseNotes | ForEach-Object { "- $_" } | Set-Content -LiteralPath (Join-Path $root 'release-notes.md') -Encoding utf8
Write-Host "Published: $output"
Write-Host "Portable archive: $portable"
