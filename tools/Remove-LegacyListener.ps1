# Remove legacy v2 PowerShell listener that steals UPSMON FindWindow.
$ErrorActionPreference = 'SilentlyContinue'
Unregister-ScheduledTask -TaskName 'UpsmonToastListener' -Confirm:$false
Write-Host 'Removed scheduled task UpsmonToastListener (if existed)'

$listeners = Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" |
    Where-Object {
        $_.ProcessId -gt 0 -and
        ($_.CommandLine -like '*UpsmonToast*' -or $_.CommandLine -like '*UpsmonToast-Listener*')
    }
if ($listeners) {
    $listeners | ForEach-Object {
        Stop-Process -Id $_.ProcessId -Force
        Write-Host "Killed PowerShell listener pid=$($_.ProcessId)"
    }
} else {
    Write-Host 'No UpsmonToast PowerShell processes found (OK)'
}

# Kill any orphan TnUPSMONProEventMesg window not owned by EventMsg.exe
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class W {
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc e, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
    [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
}
"@
$eventPid = (Get-Process EventMsg -ErrorAction SilentlyContinue | Select-Object -First 1).Id
[uint32]$killPid = 0
[W]::EnumWindows([W+EnumProc]{
    param($h,$l)
    $c = New-Object System.Text.StringBuilder 256
    [void][W]::GetClassName($h, $c, 256)
    if ($c.ToString() -ne 'TnUPSMONProEventMesg') { return $true }
    [uint32]$owner = 0
    [void][W]::GetWindowThreadProcessId($h, [ref]$owner)
    if ($owner -ne $script:eventPid -and $owner -gt 0) {
        $script:killPid = $owner
    }
    return $true
}, [IntPtr]::Zero) | Out-Null
if ($killPid -gt 0) {
    Stop-Process -Id $killPid -Force
    Write-Host "Killed orphan listener owner pid=$killPid"
}

Write-Host 'Done. Restart EventMsg.exe if needed.'
