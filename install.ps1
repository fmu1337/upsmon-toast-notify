# Install toast EventMsg.exe over UPSMON PRO stub/original (admin required).
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$installDir = 'C:\Program Files (x86)\UPSMONPRO'
$backupDir = 'C:\Users\Public\UPSMON-Pro\backup'
$dataDir = 'C:\Users\Public\UPSMON-Pro'
$built = Join-Path $here 'EventMsg.exe'

if (-not (Test-Path $built)) { & (Join-Path $here 'build.ps1') }

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) { throw 'Run PowerShell as Administrator to replace EventMsg.exe in Program Files.' }

if (-not (Test-Path $backupDir)) { New-Item -ItemType Directory -Path $backupDir -Force | Out-Null }
$target = Join-Path $installDir 'EventMsg.exe'
if (Test-Path $target) {
    $bak = Join-Path $backupDir 'EventMsg.exe'
    if (-not (Test-Path $bak) -or (Get-Item $target).Length -gt 10000) {
        Copy-Item -LiteralPath $target -Destination $bak -Force
        Write-Host "Backed up -> $bak"
    }
}

Get-Process EventMsg -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

Copy-Item -LiteralPath $built -Destination $target -Force
$toolsDest = Join-Path $installDir 'tools'
New-Item -ItemType Directory -Force -Path $toolsDest | Out-Null
Copy-Item -LiteralPath (Join-Path $here 'tools\Show-Toast.ps1') -Destination (Join-Path $toolsDest 'Show-Toast.ps1') -Force
Write-Host "Installed toast EventMsg -> $target"

$disData = Join-Path $dataDir 'DisData.Dat'
if (Test-Path -LiteralPath $disData) {
    $bytes = [IO.File]::ReadAllBytes($disData)
    if ($bytes.Length -gt 41 -and $bytes[41] -eq 0) {
        $bytes[41] = 1
        [IO.File]::WriteAllBytes($disData, $bytes)
        Write-Host 'DisData.Dat: enabled notify byte @0x29 (events -> EventMsg)'
    }
}

# Ensure popups stay enabled in ini (toast replaces modal, not silence).
$ini = 'C:\Users\Public\UPSMON-Pro\UPSMON.ini'
if (Test-Path $ini) {
    $text = Get-Content -LiteralPath $ini -Raw
    if ($text -notmatch '(?m)^\[PopMsg\]') {
        Add-Content -LiteralPath $ini -Value "`r`n[PopMsg]`r`nEnable=1`r`n"
    } else {
        $text = [regex]::Replace($text, '(?m)^Enable=0\s*$', 'Enable=1', 1)
        if ($text -ne (Get-Content -LiteralPath $ini -Raw)) { Set-Content -LiteralPath $ini -Value $text -NoNewline }
    }
}

Write-Host @"

Done.
  1. Restart UPSMON from tray (Exit -> run UPSMONPro.exe) or reboot.
  2. Test toast: & `"$target`" --test-toast
  3. Debug log: C:\Users\Public\UPSMON-Pro\event-msg.log
  4. Spy mode: & `"$target`" --spy
"@
