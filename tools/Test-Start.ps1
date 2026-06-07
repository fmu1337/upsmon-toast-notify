Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class Win32 {
    [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern IntPtr FindWindow(string cls, string wnd);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
}
"@

$hwnd = [Win32]::FindWindow('TnUPSMONProEventMesg', $null)
Write-Host "Before start: FindWindow=$hwnd IsWindow=$([Win32]::IsWindow($hwnd))"

$p = Start-Process -FilePath 'C:\Users\user\Downloads\Telegram Desktop\UPSMIONPRO\event-msg\EventMsg.exe' -ArgumentList '--spy' -WindowStyle Hidden -PassThru -Wait
Write-Host "ExitCode=$($p.ExitCode)"

$hwnd2 = [Win32]::FindWindow('TnUPSMONProEventMesg', $null)
Write-Host "After exit: FindWindow=$hwnd2 IsWindow=$([Win32]::IsWindow($hwnd2))"

Get-Content 'C:\Users\Public\UPSMON-Pro\event-msg.log' -Tail 3 -Encoding UTF8
