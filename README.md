# upsmon-toast-notify

Замена штатного `EventMsg.exe` из **UPSMON Pro** (PowerCom): вместо модальных окон — **системные toast-уведомления Windows** (справа снизу).

**Один файл:** `EventMsg.exe` — слушает сообщения UPSMON и показывает toast (без PowerShell).

---

## Как это работает

1. **UPSMONPro.exe** при событии шлёт Windows-сообщения на скрытое окно класса `TnUPSMONProEventMesg`.
2. Штатный `EventMsg.exe` показывает модальное окно с OK.
3. **Замена** создаёт то же скрытое окно, принимает те же сообщения и показывает **toast** через WinRT (встроено в exe).

Установка кладёт `EventMsg.exe` в `C:\ProgramData\UpsmonToastNotify\` и создаёт задачу **UpsmonToastListener** (автозапуск при входе). Штатный popup отключается переименованием `EventMsg.exe` → `EventMsg.exe.stock`.

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

---

## Проверка

```powershell
& "C:\ProgramData\UpsmonToastNotify\EventMsg.exe" --test-toast
```

Должен появиться toast справа снизу.

---

## Device Guard / Smart App Control

Unsigned exe **может блокироваться** Windows (WDAC, Smart App Control). Раньше обходили через PowerShell listener; теперь один exe в **ProgramData** (не в Program Files) — часто проходит, но не гарантировано.

Если `--test-toast` блокируется — разреши приложение в политике организации или отключи Smart App Control для этого файла.

---

## Откат

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1 -Uninstall
```

Или восстанови `EventMsg.exe` из `C:\Users\Public\UPSMON-Pro\backup\`.

---

## Отладка

Лог: `C:\ProgramData\UpsmonToastNotify\event-msg.log`

```powershell
& "C:\ProgramData\UpsmonToastNotify\EventMsg.exe" --spy
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
