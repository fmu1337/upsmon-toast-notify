# upsmon-toast-notify

Замена штатного **`EventMsg.exe`** из UPSMON Pro: модальные окна с кнопкой OK → **toast-уведомления Windows**.

Репозиторий: [github.com/fmu1337/upsmon-toast-notify](https://github.com/fmu1337/upsmon-toast-notify)

---

## Releases

В каждом [Release](https://github.com/fmu1337/upsmon-toast-notify/releases) два файла:

| Файл | Назначение |
|------|------------|
| **`EventMsg.exe`** | Установка в UPSMON — только работа с событиями, без тестовых ключей |
| **`EventMsg-Dev.exe`** | Отладка и проверка: `--test-toast`, `--test-toast-replace`, `--spy` |

**Текущая версия:** v3.2.0 — см. [CHANGELOG.md](CHANGELOG.md).

---

## Как это работает

UPSMON **сам** запускает `EventMsg.exe` при каждом событии (как оригинал). Процесс:

1. Читает событие (как штатный EventMsg — `EventRecord.CSV`, Timer1, скрытое окно для UPSMON).
2. Показывает toast (~25 сек, со звуком).
3. Завершается.

Модальное окно **не показывается**. Production exe **не нужно** держать в фоне вручную.

Новое событие **заменяет** предыдущий toast (Power Failure → Power Restore, Self Test → Battery Normal).

---

## Установка

1. Закрой UPSMON в трее.
2. Сохрани оригинал:

```
C:\Program Files (x86)\UPSMONPRO\EventMsg.exe
→ C:\Users\Public\UPSMON-Pro\backup\EventMsg.exe
```

3. Скачай **`EventMsg.exe`** из Releases и **разблокируй** (см. ниже), пока файл в Downloads.
4. Скопируй поверх штатного (~32 KB, x86):

```
C:\Program Files (x86)\UPSMONPRO\EventMsg.exe
```

5. В `C:\Users\Public\UPSMON-Pro\UPSMON.ini`:

```ini
[PopMsg]
Enable=1
```

6. Перезапусти UPSMON.

> **Не ставь** `EventMsg-Dev.exe` в папку UPSMON — только `EventMsg.exe`.

---

## Разблокировка (Defender / SmartScreen)

Файл с интернета помечается «из другого компьютера». Разблокируй **до** копирования в Program Files.

**Свойства файла:** ПКМ → Свойства → **Разблокировать** → OK.

**PowerShell:**

```powershell
Unblock-File -LiteralPath "$env:USERPROFILE\Downloads\EventMsg.exe"
```

**Исключение Defender** (если всё равно блокирует):

```powershell
Add-MpPreference -ExclusionPath "C:\Program Files (x86)\UPSMONPRO\EventMsg.exe"
```

**Smart App Control:** Параметры → Безопасность Windows → Smart App Control — при «Блокировке» неподписанные exe могут не запускаться.

**Device Guard (корпоративный ПК):** нужен allowlist у IT; домашние инструкции не помогут.

---

## Проверка

### Без UPSMON — через Dev-бинарник

```powershell
.\EventMsg-Dev.exe --test-toast
.\EventMsg-Dev.exe --test-toast-replace
```

- Первый — один toast со звуком.
- Второй — два toast подряд; **второй должен перебить первый** без закрытия первого.

### Через UPSMON

| Действие | Ожидаемые toast |
|----------|-----------------|
| Battery test | UPS Self Test → ~12 с → Battery Normal |
| Отключение сети от ИБП | Power Failure |
| Возврат сети | Power Restore (перебивает Failure) |

### Звук и длительность

- Toast на экране ~**25 секунд**.
- Звук — **Параметры → Система → Уведомления → UPSMON Pro** (не «без звука»).
- Режим «Не беспокоить» может заглушить звук и баннеры.

### Лог

```
C:\Users\Public\UPSMON-Pro\event-msg.log
```

```powershell
Get-Content 'C:\Users\Public\UPSMON-Pro\event-msg.log' -Tail 20
```

---

## Если уведомлений нет

1. `[PopMsg] Enable=1` в `UPSMON.ini`.
2. Уведомления для **UPSMON Pro** включены в Windows.
3. Очисти старые toast в центре уведомлений (Win+N) — после обновления с v3.1.x.
4. Нет висящего `EventMsg.exe`: `tasklist | findstr /i EventMsg` — должно быть пусто между событиями.

### Legacy listener v1/v2 (PowerShell)

Если раньше ставили старый `install.ps1`, мог остаться фоновый PowerShell с окном `TnUPSMONProEventMesg`. UPSMON шлёт события **первому** такому окну — toast не приходит.

**v3.0.6+** при старте пытается завершить такой процесс. Если не помогло — PowerShell **от администратора**:

```powershell
Unregister-ScheduledTask -TaskName UpsmonToastListener -Confirm:$false
Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" |
  Where-Object { $_.CommandLine -like '*UpsmonToast*' } |
  ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
```

Скрипт: [`tools/Remove-LegacyListener.ps1`](tools/Remove-LegacyListener.ps1)

---

## Отладка (EventMsg-Dev.exe)

| Ключ | Действие |
|------|----------|
| `--spy` | Лог Win32-сообщений в `event-msg.log`, без toast |
| `--test-toast` | Пробный toast |
| `--test-toast-replace` | Проверка замены toast |
| `--help` | Список ключей |

```powershell
.\EventMsg-Dev.exe --spy
```

---

## Откат

Верни backup:

```
C:\Users\Public\UPSMON-Pro\backup\EventMsg.exe
→ C:\Program Files (x86)\UPSMONPRO\EventMsg.exe
```

---

## Сборка

Требуется .NET Framework 4.x (`csc.exe` в `Windows\Microsoft.NET\Framework\v4.0.30319\`).

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

Создаёт:

- `EventMsg.exe` — production
- `EventMsg-Dev.exe` — dev (`/define:DEV_BUILD`)

Релизный пакет:

```powershell
powershell -ExecutionPolicy Bypass -File .\package-release.ps1
# → release\EventMsg.exe, release\EventMsg-Dev.exe
```

CI: push тега `v*` → GitHub Actions → Release с обоими exe.

---

## Структура

| Путь | Назначение |
|------|------------|
| `src/Program.cs` | Точка входа (prod / dev через `DEV_BUILD`) |
| `src/EventMessageHost.cs` | Скрытое окно UPSMON, Timer1, toast |
| `src/EventRecordReader.cs` | Чтение `EventRecord.CSV` |
| `src/StockForwarder.cs` | Передача события второму запуску UPSMON |
| `src/ToastNotifier.cs` | WinRT toast, звук, замена |
| `tools/` | Диагностика (legacy listener, окна, mutex) |

---

## Лицензия

MIT — см. [LICENSE](LICENSE).
