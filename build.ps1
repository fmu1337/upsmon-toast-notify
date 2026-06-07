# Build EventMsg.exe (production) and EventMsg-Dev.exe (debug), 32-bit x86.
$ErrorActionPreference = 'Stop'
$csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$sources = (Join-Path $here 'src\*.cs')
$manifest = Join-Path $here 'app.manifest'

if (-not (Test-Path $csc)) { throw "csc.exe not found: $csc" }

function Build-Variant {
    param(
        [string]$OutName,
        [string[]]$Defines = @()
    )

    $out = Join-Path $here $OutName
    $args = @(
        '/nologo', '/target:winexe', '/platform:x86', '/optimize+',
        '/reference:System.Windows.Forms.dll',
        '/reference:System.Drawing.dll',
        "/win32manifest:$manifest",
        "/out:$out"
    )
    foreach ($d in $Defines) { $args += "/define:$d" }
    $args += $sources

    & $csc @args
    if ($LASTEXITCODE -ne 0) { throw "build failed ($OutName) exit $LASTEXITCODE" }
    Write-Host "OK -> $out (x86)"
}

Build-Variant -OutName 'EventMsg.exe'
Build-Variant -OutName 'EventMsg-Dev.exe' -Defines @('DEV_BUILD')
