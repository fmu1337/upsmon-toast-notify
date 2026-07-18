# Changelog

## v3.2.1

- **Fix:** toast мог не появиться, если `FindWindow` находил чужое окно (PowerShell / legacy) — forward уходил туда, а свой toast не показывался.
- Сначала свой toast из `EventRecord.CSV`; forward только к живому `EventMsg.exe`.
- Rival cleanup убивает только чужие процессы, не другой EventMsg.

## v3.2.0

- Два бинарника в релизе: **`EventMsg.exe`** (production) и **`EventMsg-Dev.exe`** (тесты и `--spy`).
- Toast **~25 сек** на экране (`duration=long`).
- **Звук** системного уведомления (настраивается в Windows для UPSMON Pro).
- Новое событие **перебивает** предыдущий toast (tag/group + сброс истории).
- Production exe **без** CLI-флагов `--test-toast`, `--spy`, `--help`.

## v3.1.2

- Исправлена замена toast: Tag/Group на объекте WinRT, сброс истории перед показом.
- `--test-toast-replace` для проверки без ИБП.

## v3.1.0

- Упрощён протокол как у штатного EventMsg: скрытое окно `TnUPSMONProEventMesg`, Timer1, `EventRecord.CSV`.
- Второй запуск UPSMON передаёт событие уже работающему процессу (`StockForwarder`).

## v3.0.9

- Чтение `EventRecord.CSV` при старте (battery test / Self Test без WM_COPYDATA).
- Исправлен x86 WinRT bootstrap (`Framework`, не `Framework64`).

## v3.0.8

- Ephemeral-модель: toast и выход; пересылка при повторном запуске UPSMON.
- Очистка legacy PowerShell listener.

## v3.0.6 и ранее

- Нативный C# exe вместо PowerShell listener.
- x86 сборка под 32-bit UPSMON Pro.
