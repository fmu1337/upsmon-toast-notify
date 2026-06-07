# Install UPSMON toast notifications - single EventMsg.exe in ProgramData + scheduled task.
param([switch]$Uninstall)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$installDir = 'C:\Program Files (x86)\UPSMONPRO'
$backupDir = 'C:\Users\Public\UPSMON-Pro\backup'
$dataDir = 'C:\Users\Public\UPSMON-Pro'
$deployRoot = Join-Path $env:ProgramData 'UpsmonToastNotify'
$taskName = 'UpsmonToastListener'
$listenerExe = Join-Path $deployRoot 'EventMsg.exe'

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) { throw 'Run PowerShell as Administrator.' }

function Remove-ListenerTask {
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue | Out-Null
}

function Install-ListenerTask([string]$ExePath) {
    if (-not (Test-Path -LiteralPath $ExePath)) {
        throw "EventMsg.exe not found: $ExePath"
    }
    $action = New-ScheduledTaskAction -Execute $ExePath
    $trigger = New-ScheduledTaskTrigger -AtLogOn
    $principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Highest
    $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable
    Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null
}

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
    Remove-ListenerTask
    Get-Process -Name 'EventMsg' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    $stock = Join-Path $installDir 'EventMsg.exe.stock'
    $target = Join-Path $installDir 'EventMsg.exe'
    if (Test-Path -LiteralPath $stock) {
        Copy-Item -LiteralPath $stock -Destination $target -Force
        Remove-Item -LiteralPath $stock -Force
        Write-Host "Restored stock EventMsg.exe"
    }
    Write-Host 'Uninstalled. Deploy folder kept at:' $deployRoot
    exit 0
}

$srcExe = Join-Path $here 'EventMsg.exe'
if (-not (Test-Path -LiteralPath $srcExe)) {
    & (Join-Path $here 'build.ps1')
    $srcExe = Join-Path $here 'EventMsg.exe'
}

New-Item -ItemType Directory -Force -Path $deployRoot | Out-Null
Copy-Item -Force $srcExe $listenerExe
Unblock-File -LiteralPath $listenerExe -ErrorAction SilentlyContinue

$stockTarget = Join-Path $installDir 'EventMsg.exe'
$stockDisabled = Join-Path $installDir 'EventMsg.exe.stock'
Ensure-Backup $stockTarget

Get-Process EventMsg -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 400

if ((Test-Path -LiteralPath $stockTarget) -and -not (Test-Path -LiteralPath $stockDisabled)) {
    Move-Item -LiteralPath $stockTarget -Destination $stockDisabled -Force
    Write-Host "Disabled stock popup binary -> EventMsg.exe.stock"
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

Remove-ListenerTask
Install-ListenerTask -ExePath $listenerExe
Write-Host "Scheduled task: $taskName -> $listenerExe"

Start-Process -FilePath $listenerExe -WindowStyle Hidden

Write-Host @"

Done.
  Test toast:
    & "$listenerExe" --test-toast

  Log: $deployRoot\event-msg.log

  Restart UPSMON from tray, or reboot.

  If Windows blocks unsigned exe (Device Guard), allow EventMsg.exe in ProgramData
  or turn off Smart App Control for this app.
"@
