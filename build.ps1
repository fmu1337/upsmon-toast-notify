# Build EventMsg.exe - single binary toast replacement for UPSMON PRO (.NET 4.x, no SDK).
$ErrorActionPreference = 'Stop'
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$out = Join-Path $here 'EventMsg.exe'

if (-not (Test-Path $csc)) { throw "csc.exe not found: $csc" }

& $csc /nologo /target:winexe /platform:anycpu /optimize+ `
    /reference:System.Windows.Forms.dll `
    /reference:System.Drawing.dll `
    /win32manifest:"$here\app.manifest" `
    /out:$out `
    "$here\src\*.cs"

if ($LASTEXITCODE -ne 0) { throw "build failed ($LASTEXITCODE)" }
Write-Host "OK -> $out"
