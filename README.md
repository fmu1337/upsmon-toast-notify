# upsmon-toast-notify

Замена штатного `EventMsg.exe` из **UPSMON Pro** (PowerCom): вместо модальных окон — **системные toast-уведомления Windows**.

**Установка = один файл:** подписанный `EventMsg.exe` копируется в `C:\Program Files (x86)\UPSMONPRO\` (как штатный).

---

## Как это работает

1. **UPSMONPro.exe** шлёт Windows-сообщения на скрытое окно `TnUPSMONProEventMesg`.
2. Штатный `EventMsg.exe` показывает модальное окно.
3. **Замена** принимает те же сообщения и показывает **toast** (WinRT внутри exe).

UPSMON сам запускает `EventMsg.exe` из своей папки — отдельный планировщик и ProgramData не нужны.

---

## Установка

1. Скачай zip из [Releases](https://github.com/fmu1337/upsmon-toast-notify/releases).
2. Закрой UPSMON в трее.
3. **PowerShell от администратора**:

```powershell
cd путь\к\распакованной\папке
powershell -ExecutionPolicy Bypass -File .\install.ps1
```

Скрипт:
- ставит сертификат `UpsmonToastNotify.cer` в **Trusted Publishers** (чтобы Windows не блокировала exe);
- бэкапит оригинал в `C:\Users\Public\UPSMON-Pro\backup\`;
- копирует `EventMsg.exe` в `C:\Program Files (x86)\UPSMONPRO\`.

**Вручную** (если не нужен скрипт): импортируй `UpsmonToastNotify.cer` в «Доверенные издатели», затем скопируй `EventMsg.exe` поверх старого.

4. Перезапусти UPSMON.

---

## Проверка

```powershell
& "C:\Program Files (x86)\UPSMONPRO\EventMsg.exe" --test-toast
```

---

## Подпись

Release собирается с **Authenticode** (self-signed cert `Upsmon Toast Notify`). Для вашего ПК этого достаточно после `install.ps1`.

| Сценарий | Что делать |
|----------|------------|
| Smart App Control / «неизвестный издатель» | `install.ps1` (доверяет `.cer`) |
| Корпоративный Device Guard (WDAC) | Админ должен добавить `.cer` или хеш exe в политику |
| Нужен «зелёный» SmartScreen | Нужен коммерческий EV-сертификат (~$300+/год) |

---

## Откат

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1 -Uninstall
```

Или восстанови файл из `C:\Users\Public\UPSMON-Pro\backup\EventMsg.exe`.

---

## Отладка

Лог: `C:\Users\Public\UPSMON-Pro\event-msg.log`

```powershell
& "C:\Program Files (x86)\UPSMONPRO\EventMsg.exe" --spy
```

---

## Сборка

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Sign
powershell -ExecutionPolicy Bypass -File .\package-release.ps1
```

---

## Лицензия

MIT — см. [LICENSE](LICENSE).
