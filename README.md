# upsmon-toast-notify

Замена штатного `EventMsg.exe` из **UPSMON Pro** (PowerCom): вместо модальных окон — **системные toast-уведомления Windows** (справа снизу).

---

## Как это работает

1. **UPSMONPro.exe** при событии шлёт Windows-сообщения на скрытое окно класса `TnUPSMONProEventMesg`.
2. Штатный `EventMsg.exe` показывает модальное окно с OK.
3. **Замена** создаёт то же скрытое окно, принимает те же сообщения и показывает **toast** через PowerShell + WinRT (`Show-Toast.ps1`).

По умолчанию используется **режим listener** (Scheduled Task + PowerShell) — работает при **Device Guard / Smart App Control**, когда unsigned `EventMsg.exe` блокируется.

---

## Установка

1. Скачай zip из [Releases](https://github.com/fmu1337/upsmon-toast-notify/releases).
2. Распакуй, закрой UPSMON в трее.
3. **PowerShell от администратора**:

```powershell
cd путь\к\распакованной\папке
powershell -ExecutionPolicy Bypass -File .\install.ps1
```

4. Перезапусти `UPSMONPro.exe` или перезагрузи ПК.

Скрипт:
- кладёт файлы в `C:\ProgramData\UpsmonToastNotify\`;
- создаёт задачу **UpsmonToastListener** (запуск при входе);
- переименовывает штатный `EventMsg.exe` → `EventMsg.exe.stock` (чтобы не всплывали старые окна);
- включает `[PopMsg] Enable=1` при необходимости.

---

## Проверка (без EventMsg.exe)

```powershell
powershell -ExecutionPolicy Bypass -File "C:\ProgramData\UpsmonToastNotify\tools\Test-Toast.ps1"
```

---

## Device Guard / «blocked by your organization's Device Guard policy»

Unsigned `EventMsg.exe` в `Program Files` часто **блокируется** Windows (Smart App Control, WDAC).

**Не запускай** `EventMsg.exe --test-toast` — используй `Test-Toast.ps1` выше.

Переустанови в режиме listener (по умолчанию):

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1
```

Если раньше ставил exe в Program Files — listener отключит его и возьмёт сообщения на себя.

Опционально (часто тоже блокируется):

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1 -InstallExe
```

---

## Откат

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1 -Uninstall
```

Или вручную восстанови `EventMsg.exe` из `C:\Users\Public\UPSMON-Pro\backup\`.

---

## Отладка

Лог: `C:\ProgramData\UpsmonToastNotify\event-msg.log`

```powershell
powershell -ExecutionPolicy Bypass -File "C:\ProgramData\UpsmonToastNotify\tools\UpsmonToast-Listener.ps1" -Spy
```

---

## Сборка

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
powershell -ExecutionPolicy Bypass -File .\package-release.ps1
```

---

## Лицензия

MIT — см. [LICENSE](LICENSE).
