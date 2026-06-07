$m = New-Object System.Threading.Mutex($false, 'Global\UpsmonEventMsgToast')
$got = $m.WaitOne(0)
Write-Host "mutex acquired immediately (was free): $got"
if ($got) { $m.ReleaseMutex() }
$m.Dispose()

$exe = 'C:\Users\user\Downloads\Telegram Desktop\UPSMIONPRO\event-msg\EventMsg.exe'
$p = Start-Process -FilePath $exe -WindowStyle Hidden -PassThru
Start-Sleep -Seconds 2
Write-Host "Started pid=$($p.Id) hasExited=$($p.HasExited)"
Get-Process -Name EventMsg -ErrorAction SilentlyContinue | Select-Object Id,ProcessName
Get-Content 'C:\Users\Public\UPSMON-Pro\event-msg.log' -Tail 5 -Encoding UTF8
