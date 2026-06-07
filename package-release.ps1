# Builds EventMsg.exe + EventMsg-Dev.exe for GitHub Release.
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
& (Join-Path $here 'build.ps1')

$releaseDir = Join-Path $here 'release'
New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null

$version = '3.2.0'
Copy-Item (Join-Path $here 'EventMsg.exe') (Join-Path $releaseDir 'EventMsg.exe') -Force
Copy-Item (Join-Path $here 'EventMsg-Dev.exe') (Join-Path $releaseDir 'EventMsg-Dev.exe') -Force
Write-Host "Release v$version ->"
Write-Host "  $(Join-Path $releaseDir 'EventMsg.exe')      (install to UPSMON)"
Write-Host "  $(Join-Path $releaseDir 'EventMsg-Dev.exe')  (tests / --spy)"
