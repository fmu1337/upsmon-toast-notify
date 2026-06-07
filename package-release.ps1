# Builds EventMsg.exe and packs a release zip for GitHub.
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
& (Join-Path $here 'build.ps1')

$releaseDir = Join-Path $here 'release'
New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null

$version = '1.0.0'
$stage = Join-Path $releaseDir "upsmon-toast-notify-$version"
if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'tools') | Out-Null

Copy-Item (Join-Path $here 'EventMsg.exe') $stage -Force
Copy-Item (Join-Path $here 'install.ps1') $stage -Force
Copy-Item (Join-Path $here 'tools\Show-Toast.ps1') (Join-Path $stage 'tools') -Force
Copy-Item (Join-Path $here 'README.md') $stage -Force

$zip = Join-Path $releaseDir "upsmon-toast-notify-$version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force
Write-Host "Release zip -> $zip"
