# Install UPSMON toast notifications (listener mode — works with Device Guard / Smart App Control).
param(
    [switch]$InstallExe,
    [switch]$Uninstall
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$installDir = 'C:\Program Files (x86)\UPSMONPRO'
$backupDir = 'C:\Users\Public\UPSMON-Pro\backup'
$dataDir = 'C:\Users\Public\UPSMON-Pro'
$deployRoot = Join-Path $env:ProgramData 'UpsmonToastNotify'
$taskName = 'UpsmonToastListener'
$listener = Join-Path $deployRoot 'tools\UpsmonToast-Listener.ps1'

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) { throw 'Run PowerShell as Administrator.' }

function Ensure-Backup([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return }
    if (-not (Test-Path -LiteralPath $backupDir)) { New-Item -ItemType Directory -Force -Path $backupDir | Out-Null }
    $bak = Join-Path $backupDir (Split-Path $Path -Leaf)
    if (-not (Test-Path -LiteralPath $bak)) {
        Copy-Item -LiteralPath $Path -Destination $bak -Force
        Write-Host "Backed up -> $bak"
    }
}

if ($Uninstall) {
    schtasks /delete /tn $taskName /f 2>$null | Out-Null
    Get-Process -Name 'powershell' -ErrorAction SilentlyContinue | Where-Object {
        try { $_.CommandLine -like '*UpsmonToast-Listener.ps1*' } catch { $false }
    } | Stop-Process -Force -ErrorAction SilentlyContinue
    $stock = Join-Path $installDir 'EventMsg.exe.stock'
    $target = Join-Path $installDir 'EventMsg.exe'
    if (Test-Path -LiteralPath $stock) {
        Copy-Item -LiteralPath $stock -Destination $target -Force
        Remove-Item -LiteralPath $stock -Force
        Write-Host "Restored stock EventMsg.exe"
    }
    Write-Host 'Uninstalled listener task. Deploy folder kept at:' $deployRoot
    exit 0
}

# Deploy files to ProgramData (not blocked by Device Guard).
New-Item -ItemType Directory -Force -Path (Join-Path $deployRoot 'tools') | Out-Null
Copy-Item -Force (Join-Path $here 'tools\Show-Toast.ps1') (Join-Path $deployRoot 'tools') 
Copy-Item -Force (Join-Path $here 'tools\Test-Toast.ps1') (Join-Path $deployRoot 'tools')
Copy-Item -Force (Join-Path $here 'tools\UpsmonToast-Listener.ps1') (Join-Path $deployRoot 'tools')
if (Test-Path (Join-Path $here 'EventMsg.exe')) {
    Copy-Item -Force (Join-Path $here 'EventMsg.exe') $deployRoot
}

$stockTarget = Join-Path $installDir 'EventMsg.exe'
$stockDisabled = Join-Path $installDir 'EventMsg.exe.stock'
Ensure-Backup $stockTarget

Get-Process EventMsg -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 400

if (-not $InstallExe) {
    # Listener mode: disable stock popup binary so UPSMON does not show modal dialogs.
    if ((Test-Path -LiteralPath $stockTarget) -and -not (Test-Path -LiteralPath $stockDisabled)) {
        Move-Item -LiteralPath $stockTarget -Destination $stockDisabled -Force
        Write-Host "Disabled stock popup binary -> EventMsg.exe.stock"
    }
    Write-Host 'Listener mode (Device Guard safe): toast via Scheduled Task + PowerShell'
} else {
    # Optional: replace EventMsg.exe (may be blocked by Device Guard on some PCs).
    if (-not (Test-Path (Join-Path $here 'EventMsg.exe'))) { & (Join-Path $here 'build.ps1') }
    Copy-Item -Force (Join-Path $here 'EventMsg.exe') $stockTarget
    Unblock-File -LiteralPath $stockTarget -ErrorAction SilentlyContinue
    $toolsDest = Join-Path $installDir 'tools'
    New-Item -ItemType Directory -Force -Path $toolsDest | Out-Null
    Copy-Item -Force (Join-Path $deployRoot 'tools\Show-Toast.ps1') (Join-Path $toolsDest 'Show-Toast.ps1')
    Write-Host "Installed EventMsg.exe -> $stockTarget (may require Smart App Control off if blocked)"
}

$disData = Join-Path $dataDir 'DisData.Dat'
if (Test-Path -LiteralPath $disData) {
    $bytes = [IO.File]::ReadAllBytes($disData)
    if ($bytes.Length -gt 41 -and $bytes[41] -eq 0) {
        $bytes[41] = 1
        [IO.File]::WriteAllBytes($disData, $bytes)
        Write-Host 'DisData.Dat: notify channel enabled @0x29'
    }
}

$ini = Join-Path $dataDir 'UPSMON.ini'
if (Test-Path $ini) {
    $text = Get-Content -LiteralPath $ini -Raw
    if ($text -notmatch '(?m)^\[PopMsg\]') {
        Add-Content -LiteralPath $ini -Value "`r`n[PopMsg]`r`nEnable=1`r`n"
    } else {
        $text2 = [regex]::Replace($text, '(?m)^Enable=0\s*$', 'Enable=1', 1)
        if ($text2 -ne $text) { Set-Content -LiteralPath $ini -Value $text2 -NoNewline }
    }
}

$tr = "powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$listener`""
schtasks /delete /tn $taskName /f 2>$null | Out-Null
schtasks /create /tn $taskName /sc onlogon /rl HIGHEST /tr $tr /f | Out-Null
Write-Host "Scheduled task: $taskName"

Start-Process powershell.exe -ArgumentList @(
    '-NoProfile', '-WindowStyle', 'Hidden', '-ExecutionPolicy', 'Bypass',
    '-File', $listener
) -WindowStyle Hidden

Write-Host @"

Done.
  Test toast (no EventMsg.exe needed):
    powershell -ExecutionPolicy Bypass -File "$deployRoot\tools\Test-Toast.ps1"

  Log: $deployRoot\..\UpsmonToastNotify\event-msg.log
       (or C:\ProgramData\UpsmonToastNotify\event-msg.log)

  Restart UPSMON from tray, or reboot.

  If EventMsg.exe was blocked by Device Guard — this is expected; use listener mode above.
  Optional exe install (often blocked): install.ps1 -InstallExe
"@
