Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class Win32 {
    public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint procId);
    [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
    [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpText, int nMaxCount);
    [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern IntPtr FindWindow(string cls, string wnd);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
}
"@

$eventPid = (Get-Process EventMsg -ErrorAction SilentlyContinue | Select-Object -First 1).Id
Write-Host "EventMsg pid=$eventPid"

$matches = New-Object System.Collections.Generic.List[string]
[Win32]::EnumWindows([Win32+EnumProc]{
    param($h, $l)
    $sbC = New-Object System.Text.StringBuilder 256
    $sbT = New-Object System.Text.StringBuilder 256
    [void][Win32]::GetClassName($h, $sbC, $sbC.Capacity)
    if ($sbC.ToString() -ne 'TnUPSMONProEventMesg') { return $true }
    [void][Win32]::GetWindowText($h, $sbT, $sbT.Capacity)
    [uint32]$owner = 0
    [void][Win32]::GetWindowThreadProcessId($h, [ref]$owner)
    $script:matches.Add("hwnd=$h owner=$owner title=$($sbT) IsWindow=$([Win32]::IsWindow($h))")
    return $true
}, [IntPtr]::Zero) | Out-Null

if ($matches.Count -eq 0) { Write-Host 'No TnUPSMONProEventMesg windows' }
else { $matches | ForEach-Object { Write-Host $_ } }

$fw = [Win32]::FindWindow('TnUPSMONProEventMesg', 'UPSMON PRO Event Message')
Write-Host "FindWindow=$fw"
