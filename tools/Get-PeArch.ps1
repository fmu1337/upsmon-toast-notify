function Get-PeMachine([string]$Path) {
    $b = [IO.File]::ReadAllBytes($Path)
    $e = [BitConverter]::ToUInt16($b, 0x3C)
    $pe = [BitConverter]::ToInt32($b, $e)
    return [BitConverter]::ToUInt16($b, $pe + 4)
}

$paths = @(
    'C:\Program Files (x86)\UPSMONPRO\EventMsg.exe',
    'C:\Program Files (x86)\UPSMONPRO\UPSMONPro.exe',
    'C:\Users\Public\UPSMON-Pro\backup\EventMsg.exe'
)
foreach ($p in $paths) {
    if (Test-Path -LiteralPath $p) {
        $m = Get-PeMachine $p
        $arch = switch ($m) { 0x014c { 'x86' } 0x8664 { 'x64' } default { "0x$($m.ToString('X4'))" } }
        Write-Host "$arch  $p"
    }
}
