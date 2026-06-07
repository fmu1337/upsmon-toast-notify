# upsmon-toast-notify

Замена `EventMsg.exe` из **UPSMON Pro**: модальные окна → **toast-уведомления Windows**.

В [Releases](https://github.com/fmu1337/upsmon-toast-notify/releases) лежит один файл — **`EventMsg.exe`**.

---

## Установка

1. Закрой UPSMON в трее.
2. Сохрани оригинал (на всякий случай):

```
C:\Program Files (x86)\UPSMONPRO\EventMsg.exe
→ C:\Users\Public\UPSMON-Pro\backup\EventMsg.exe
```

3. Скачай `EventMsg.exe` из Releases и **разблокируй** (см. ниже), пока файл ещё в Downloads.
4. Скопируй поверх штатного (размер ~32 KB, не старый stub ~23 KB):

```
C:\Program Files (x86)\UPSMONPRO\EventMsg.exe
```

5. Перезапусти UPSMON.

**Важно:** `EventMsg.exe` должен работать в фоне (UPSMON запускает его сам при событии). Если toast не приходит — один раз запусти вручную и оставь:

```powershell
Start-Process "C:\Program Files (x86)\UPSMONPRO\EventMsg.exe" -WindowStyle Hidden
```

Потом снова battery test.

---

## Разблокировка (Windows Defender / SmartScreen)

Файл с интернета помечается как «из другого компьютера». Без разблокировки Windows может не запускать exe.

### Способ 1 — свойства файла

1. ПКМ по `EventMsg.exe` → **Свойства**.
2. Внизу: **Разблокировать** (Unblock) → **ОК**.
3. Потом копируй в папку UPSMON.

### Способ 2 — PowerShell

```powershell
Unblock-File -LiteralPath "$env:USERPROFILE\Downloads\EventMsg.exe"
```

Путь замени на свой. Команду выполни **до** копирования в Program Files.

### Способ 3 — исключение в Defender

Если после копирования exe всё равно блокируется:

**Параметры → Конфиденциальность и защита → Безопасность Windows → Защита от вирусов** → **Управление параметрами** → **Исключения** → **Добавить исключение** → **Файл**:

```
C:\Program Files (x86)\UPSMONPRO\EventMsg.exe
```

Или PowerShell **от администратора**:

```powershell
Add-MpPreference -ExclusionPath "C:\Program Files (x86)\UPSMONPRO\EventMsg.exe"
```

### Smart App Control

**Параметры → Конфиденциальность и защита → Безопасность Windows → Управление приложениями и браузером → Параметры Smart App Control**

Если включено «Блокировка» — неподписанные exe часто не запускаются. Варианты: **Выключить** Smart App Control или добавить исключение (если политика позволяет).

### Device Guard (корпоративный ПК)

Сообщение *«blocked by your organization's Device Guard policy»* — это политика организации, не Defender. Нужен allowlist у IT-админа (путь или хеш файла). Домашние инструкции выше не помогут.

---

## Проверка

```powershell
& "C:\Program Files (x86)\UPSMONPRO\EventMsg.exe" --test-toast
```

Должен появиться toast справа снизу.

---

## Если уведомлений нет

В `C:\Users\Public\UPSMON-Pro\UPSMON.ini` должна быть секция:

```ini
[PopMsg]
Enable=1
```

Перезапусти UPSMON после замены exe.

---

## Откат

Верни файл из `C:\Users\Public\UPSMON-Pro\backup\EventMsg.exe`.

---

## Отладка

Лог: `C:\Users\Public\UPSMON-Pro\event-msg.log`

```powershell
& "C:\Program Files (x86)\UPSMONPRO\EventMsg.exe" --spy
```

---

## Сборка

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

---

## Лицензия

MIT — см. [LICENSE](LICENSE).
