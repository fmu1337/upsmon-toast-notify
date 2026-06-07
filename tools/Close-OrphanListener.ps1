# Close orphan TnUPSMONProEventMesg windows not owned by EventMsg.exe (needs admin for some PIDs).
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class W {
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc e, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
    [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern IntPtr FindWindow(string c, string t);
}
"@
$eventPid = (Get-Process EventMsg -ErrorAction SilentlyContinue | Select-Object -First 1).Id
Write-Host "EventMsg pid=$eventPid"
$closed = 0
[W]::EnumWindows([W+EnumProc]{
    param($h,$l)
    $c = New-Object System.Text.StringBuilder 256
    [void][W]::GetClassName($h, $c, 256)
    if ($c.ToString() -ne 'TnUPSMONProEventMesg') { return $true }
    [uint32]$owner = 0
    [void][W]::GetWindowThreadProcessId($h, [ref]$owner)
    if ($script:eventPid -and $owner -eq $script:eventPid) { return $true }
    [void][W]::PostMessage($h, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) # WM_CLOSE
    Write-Host "Posted WM_CLOSE to hwnd=$h owner=$owner"
    $script:closed++
    return $true
}, [IntPtr]::Zero) | Out-Null
Start-Sleep 1
$fw = [W]::FindWindow('TnUPSMONProEventMesg', 'UPSMON PRO Event Message')
Write-Host "FindWindow after cleanup=$fw closed=$closed"
