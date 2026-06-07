# Builds EventMsg.exe for GitHub Release (exe only).
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
& (Join-Path $here 'build.ps1')

$releaseDir = Join-Path $here 'release'
New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null

$version = '3.0.0'
Copy-Item (Join-Path $here 'EventMsg.exe') (Join-Path $releaseDir 'EventMsg.exe') -Force
Write-Host "Release -> $(Join-Path $releaseDir 'EventMsg.exe') (v$version)"
