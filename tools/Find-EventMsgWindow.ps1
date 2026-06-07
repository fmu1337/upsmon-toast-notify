Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class Win32 {
    [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern IntPtr FindWindow(string cls, string wnd);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
}
"@

$proc = Get-Process EventMsg -ErrorAction SilentlyContinue | Select-Object -First 1
Write-Host "EventMsg pid=$($proc.Id)"
$hwnd = [Win32]::FindWindow('TnUPSMONProEventMesg', $null)
Write-Host "FindWindow(class)=$hwnd IsWindow=$([Win32]::IsWindow($hwnd))"
$hwnd2 = [Win32]::FindWindow('TnUPSMONProEventMesg', 'UPSMON PRO Event Message')
Write-Host "FindWindow(class+title)=$hwnd2"
