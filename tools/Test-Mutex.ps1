$created = $false
$m = New-Object System.Threading.Mutex($true, 'Global\UpsmonEventMsgToast', [ref]$created)
Write-Host "createdNew=$created (true=we own mutex, false=another process owns it)"
if ($created) {
    Write-Host "Releasing test mutex"
    $m.ReleaseMutex()
}
$m.Dispose()

$created2 = $false
$m2 = New-Object System.Threading.Mutex($true, 'Global\UpsmonEventMsgToast', [ref]$created2)
Write-Host "after dispose createdNew=$created2"
if ($created2) { $m2.ReleaseMutex() }
$m2.Dispose()
