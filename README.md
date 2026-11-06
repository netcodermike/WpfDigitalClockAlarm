# Precision Digital Clock, Alarm & Task Planner

A Windows WPF desktop clock with synthesized alarm sounds, snooze, and a persistent task list with priorities, deadlines, and daily reminders. Built with .NET 10; no third-party runtime packages, audio downloads, accounts, or cloud services.

## Features

- Live local time and date, blinking separators, and 12/24-hour display.
- One-shot alarm with three tones, test beep, custom label, quick presets, and five-minute snooze.
- Tasks with notes, four priority levels, deadline date and time, completion, deletion, and filters.
- An in-app banner and chime once per local calendar day for each unfinished task with seven days or less remaining. Overdue tasks continue reminding until completed.
- Local JSON storage, atomic replacement, a previous-save backup, and visible read/write errors.

## Requirements and quick start

Windows 10/11 and the .NET 10 SDK with Windows desktop support. From the repository root:

```powershell
dotnet build WpfDigitalClockAlarm.Tests/WpfDigitalClockAlarm.Tests.csproj -c Release
dotnet run --project WpfDigitalClockAlarm/WpfDigitalClockAlarm.csproj -c Release
```

Open the application project in a compatible Visual Studio or Rider installation if preferred.

## Use

Select an alarm time and tone, then **ARM ALARM**, or choose a quick timer preset. When it rings, dismiss or snooze for five minutes. The format button changes the clock and alarm display. Task entry always uses 12-hour time with its own AM/PM and minute selectors.

In **Tasks & Reminders**, enter a title, priority, deadline date, hour, minute, and AM/PM. Add optional notes, then select **Add Task**. Use the checkbox to complete a task, the trash button to delete it, and the filters to find active, reminder, or completed tasks.

**Keep the application running and Windows awake for timely alarms and reminders.** There are no background services or Windows toast notifications. Closing the window stops the alarm and exits. After sleep, overdue armed alarms fire when the UI timer resumes; task reminders are checked on startup and while running. Alarm settings are not persisted. Reminders use local wall-clock dates and continue for overdue tasks. The clock reads system time on each tick; its 200 ms dispatcher interval does not guarantee real-time accuracy. System clock, time-zone, and daylight-saving changes can affect schedules.

## Task storage and recovery

Tasks are stored at `%LOCALAPPDATA%/WpfDigitalClockAlarm/tasks.json`. Each successful replacement keeps the preceding saved file at `tasks.json.bak`. A new installation starts with an empty task list. There is no network sync or telemetry in application code.

If loading fails, the original file is preserved and task creation/saving is disabled for that session. Close the app, copy the original file somewhere safe, repair its JSON or restore the backup, and restart. Save failures appear in the window; keep the app open, fix the directory/permission issue, and retry a task action. Run only one instance against the same task file; concurrent processes are not coordinated. Tasks are plaintext, so do not put secrets in them.

## Tests

```powershell
dotnet run --project WpfDigitalClockAlarm.Tests/WpfDigitalClockAlarm.Tests.csproj -c Release
```

The dependency-free console test runner exits nonzero on failure. Use `dotnet run`, since this is not a VSTest project. Tests run with a WPF STA dispatcher, silent reminder audio, isolated storage under the test build directory, and disabled clock timers. They never read or write your real task file. See [review notes](docs/REVIEW.md) for findings and validation. GitHub Actions builds and runs this suite on Windows.

## Publish a Windows build

```powershell
dotnet publish WpfDigitalClockAlarm/WpfDigitalClockAlarm.csproj -c Release -r win-x64 --self-contained true -o artifacts/win-x64
```

Share the complete output directory as a ZIP. The app is unsigned; no installer or automatic updates are included. Framework-dependent builds can omit `--self-contained true` and require the matching .NET Desktop Runtime on the recipient's PC.

## Code map

| Path | Responsibility |
| --- | --- |
| `WpfDigitalClockAlarm/MainWindow.xaml` | Clock, alarm, tasks, and reminder UI |
| `WpfDigitalClockAlarm/ViewModels/MainViewModel.cs` | Commands, alarm state, timer, and reminders |
| `WpfDigitalClockAlarm/Models/TaskItem.cs` | Persisted task data and computed status |
| `WpfDigitalClockAlarm/Services/TaskStorageService.cs` | Serialized JSON writes and recovery backup |
| `WpfDigitalClockAlarm/Services/BeepService.cs` | PCM WAV synthesis and alarm playback |
| `WpfDigitalClockAlarm.Tests/` | Standalone regression runner |

## Contributing

Open an issue with reproduction steps or a pull request with a focused change. Run the build and test commands above. Do not commit task data, credentials, build output, or IDE state. See [security reporting](SECURITY.md). No open-source license has been selected yet; public visibility alone does not grant reuse rights.
