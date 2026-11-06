# Code review and improvements

Reviewed November 5, 2026 from the supplied WPF project and companion tests.

## Findings addressed

1. **Completion reversed itself:** the checkbox changed the model through a two-way binding and the command toggled it again. The binding now reflects state one-way; the command performs the single change and saves it.
2. **Missed reminder threshold and duplicate chimes:** checks ran only on date rollover, used the real system time instead of their supplied timestamp, and could overlap during audio playback. Checks now run every second, compare the deadline against the supplied timestamp, and guard against reentry. Tasks are marked before asynchronous playback.
3. **Silent data loss:** storage swallowed all failures and substituted demo tasks for unreadable data. A fresh install now starts empty, invalid data is preserved, errors are shown, and loading must succeed before saving. Writes are serialized, staged in the same directory, atomically replaced, and retain the previous version as a backup.
4. **Early presets/snooze:** target initialization dropped seconds. It now retains the target second.
5. **Task time format coupling:** switching the clock to 24-hour time exposed invalid task hours despite a separate AM/PM selector. Task hours now have an independent 1–12 list. Added the missing task-minute input.
6. **Stale task statuses:** computed properties and filters now refresh during operation; deadline/completion changes also notify overdue state.
7. **Test side effects:** tests previously loaded/saved production task data, played sounds, started timers, and lost STA context after awaits. Tests now inject isolated storage and silent reminder playback, disable timers, and use an STA dispatcher pump.
8. **Audio state race:** an old cancelled alarm loop could clear the state of a newly started loop. Cleanup now checks source identity; token sources are disposed by their owning loop. Waveform input bounds prevent invalid or excessive allocations.
9. **Readability:** native light ComboBox/DatePicker surfaces now use readable dark text. Removed extra TextBox content margins that clipped text.

## Validation

- Release build: zero warnings and zero errors.
- Expanded regression suite covers original behavior plus supplied-time threshold boundaries, overlapping reminders, storage round trip/backup, corrupt-file protection, preset/snooze timing, and independent task-hour options.
- Manual desktop inspection of both alarm and task screens; additional UI checks recorded in the delivery chat.

## Remaining scope

This is an in-process Windows utility, not a background scheduler. No tray mode, startup registration, notification history, encrypted storage, multi-process coordination, installer, signing, or persisted alarm settings. Local wall-clock scheduling inherits system clock/time-zone/DST behavior. Sound depends on Windows audio settings and hardware; real audio audibility and sleep/resume timing require a manual check on the target machine. The short synchronous audio buffers may finish playing after cancellation. The original code remains in its source location; this repository is the reviewed copy.
