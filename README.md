# upsmon-toast-notify

Замена штатного `EventMsg.exe` из **UPSMON Pro** (PowerCom): вместо модальных окон, которые перехватывают фокус, — **системные toast-уведомления Windows** (справа снизу, Action Center).

Требуется уже установленный UPSMON Pro. Это не отдельный монитор ИБП — только другой способ показа тех же событий.

---

## Как это работает

1. **UPSMONPro.exe** при событии (пропадание сети, низкий заряд и т.д.) запускает `EventMsg.exe` и шлёт ему Windows-сообщения на скрытое окно класса `TnUPSMONProEventMesg`.
2. Оригинальный Delphi-`EventMsg` рисует форму с кнопкой OK и блокирует работу.
3. **Эта замена** создаёт то же скрытое окно с тем же классом, принимает те же сообщения (`WM_COPYDATA` с пакетом `PCM…`, зарегистрированные сообщения вроде `PROStart` / `DelayEvent`) и вместо формы вызывает **Windows toast** через `Show-Toast.ps1` (WinRT).
4. Журнал событий UPSMON (`EventRecord.CSV`) по-прежнему пишет основной процесс — мы меняем **только способ уведомления пользователя**.

Оповещения должны быть **включены** в UPSMON: `[PopMsg] Enable=1` или галочка «Всплывающие сообщения» в GUI. Toast **заменяет** окно, а не отключает события.

---

## Установка из релиза (рекомендуется)

1. Скачай **`upsmon-toast-notify-1.0.0.zip`** из [Releases](https://github.com/fmu1337/upsmon-toast-notify/releases).
2. Распакуй в любую папку (например `C:\Tools\upsmon-toast-notify\`).
3. Закрой UPSMON в трее (Exit).
4. Запусти **PowerShell от имени администратора**:

```powershell
cd C:\Tools\upsmon-toast-notify
powershell -ExecutionPolicy Bypass -File .\install.ps1
```

5. Снова запусти `UPSMONPro.exe` (или перезагрузи ПК).

Скрипт:
- сохранит оригинальный `EventMsg.exe` в `C:\Users\Public\UPSMON-Pro\backup\` (если ещё не сохранён);
- скопирует новый `EventMsg.exe` в `C:\Program Files (x86)\UPSMONPRO\`;
- положит `tools\Show-Toast.ps1` рядом с установкой UPSMON;
- при необходимости включит канал оповещений в `DisData.Dat` / `[PopMsg]`.

---

## Проверка

```powershell
& "C:\Program Files (x86)\UPSMONPRO\EventMsg.exe" --test-toast
```

Должен появиться тестовый toast. Если нет — один раз открой «Параметры → Система → Уведомления» и разреши уведомления для ярлыка **UPSMON Notifications** (создаётся при первом toast).

---

## Ручная установка (без скрипта)

1. Останови процесс `EventMsg` (если запущен).
2. Сделай копию `C:\Program Files (x86)\UPSMONPRO\EventMsg.exe`.
3. Скопируй `EventMsg.exe` из архива релиза поверх старого файла.
4. Создай `C:\Program Files (x86)\UPSMONPRO\tools\` и скопируй туда `Show-Toast.ps1`.
5. Перезапусти UPSMON.

---

## Откат на оригинал

Восстанови файл из бэкапа:

```powershell
Copy-Item "C:\Users\Public\UPSMON-Pro\backup\EventMsg.exe" `
  "C:\Program Files (x86)\UPSMONPRO\EventMsg.exe" -Force
```

(или из своей копии до установки)

---

## Отладка

Лог: `C:\Users\Public\UPSMON-Pro\event-msg.log`

Режим перехвата сообщений без toast (для диагностики):

```powershell
& "C:\Program Files (x86)\UPSMONPRO\EventMsg.exe" --spy
```

---

## Сборка из исходников

Windows 10/11, встроенный .NET Framework 4.x (компилятор `csc.exe`).

```powershell
git clone git@github.com:fmu1337/upsmon-toast-notify.git
cd upsmon-toast-notify
powershell -ExecutionPolicy Bypass -File .\build.ps1
powershell -ExecutionPolicy Bypass -File .\package-release.ps1
```

Архив релиза: `release\upsmon-toast-notify-1.0.0.zip`

---

## Ограничения

- Только Windows 10/11 (toast API).
- Нужен установленный UPSMON Pro; путь по умолчанию `C:\Program Files (x86)\UPSMONPRO\`.
- Кнопка «Cancel Shutdown» из оригинального окна в toast **не дублируется** — отмена shutdown по-прежнему через GUI UPSMON / настройки.

---

## Лицензия

MIT — см. [LICENSE](LICENSE).
