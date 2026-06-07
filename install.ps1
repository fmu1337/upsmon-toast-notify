# Replace UPSMON Pro EventMsg.exe with signed toast version (simple file copy).
param(
    [switch]$Uninstall,
    [switch]$SkipTrust
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$installDir = 'C:\Program Files (x86)\UPSMONPRO'
$backupDir = 'C:\Users\Public\UPSMON-Pro\backup'
$dataDir = 'C:\Users\Public\UPSMON-Pro'
$target = Join-Path $installDir 'EventMsg.exe'
$srcExe = Join-Path $here 'EventMsg.exe'
$cerFile = Join-Path $here 'UpsmonToastNotify.cer'
$taskName = 'UpsmonToastListener'

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) { throw 'Run PowerShell as Administrator.' }

function Remove-LegacyListenerTask {
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue | Out-Null
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

function Install-TrustCertificate([string]$CerPath) {
    if (-not (Test-Path -LiteralPath $CerPath)) {
        Write-Host 'No UpsmonToastNotify.cer in package - skip trust (exe may be blocked by Smart App Control).'
        return
    }
    Import-Certificate -FilePath $CerPath -CertStoreLocation 'Cert:\LocalMachine\Root' | Out-Null
    Import-Certificate -FilePath $CerPath -CertStoreLocation 'Cert:\LocalMachine\TrustedPublisher' | Out-Null
    Write-Host 'Trusted publisher certificate installed on this PC.'
}

if ($Uninstall) {
    Remove-LegacyListenerTask
    Get-Process -Name 'EventMsg' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    $bak = Join-Path $backupDir 'EventMsg.exe'
    if (Test-Path -LiteralPath $bak) {
        Copy-Item -LiteralPath $bak -Destination $target -Force
        Write-Host "Restored stock EventMsg.exe from backup"
    } else {
        Write-Host 'No backup found at' $bak
    }
    exit 0
}

if (-not (Test-Path -LiteralPath $srcExe)) {
    & (Join-Path $here 'build.ps1')
    & (Join-Path $here 'sign.ps1') -FilePath (Join-Path $here 'EventMsg.exe') -ExportCert
    $srcExe = Join-Path $here 'EventMsg.exe'
    $cerFile = Join-Path $here 'UpsmonToastNotify.cer'
}

Remove-LegacyListenerTask
Ensure-Backup $target

Get-Process EventMsg -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 400

if (-not $SkipTrust) {
    Install-TrustCertificate -CerPath $cerFile
}

Copy-Item -LiteralPath $srcExe -Destination $target -Force
Unblock-File -LiteralPath $target -ErrorAction SilentlyContinue
Write-Host "Installed -> $target"

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

Start-Process -FilePath $target -WindowStyle Hidden

Write-Host @"

Done.
  Test toast:
    & "$target" --test-toast

  Log: $dataDir\event-msg.log

  Restart UPSMON from tray, or reboot.

  Certificate: $cerFile (already trusted unless -SkipTrust was used)
"@
