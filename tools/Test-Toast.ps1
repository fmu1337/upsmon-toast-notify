# Test toast without EventMsg.exe (works when Device Guard blocks unsigned exe).
param(
    [string]$Title = 'UPSMON Pro',
    [string]$Body = 'Тест: уведомление Windows вместо всплывающего окна'
)
$ErrorActionPreference = 'Stop'
$script = Join-Path $PSScriptRoot 'Show-Toast.ps1'
if (-not (Test-Path -LiteralPath $script)) { throw "Show-Toast.ps1 not found: $script" }
& $script -Title $Title -Body $Body
