using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WpfDigitalClockAlarm.Models;
using WpfDigitalClockAlarm.Services;
using WpfDigitalClockAlarm.ViewModels;

namespace WpfDigitalClockAlarm.Tests
{
    public class UnitTests
    {
        private sealed class SilentAudio : BeepService
        {
            public override Task PlayReminderChimeAsync() => Task.CompletedTask;
        }

        private static MainViewModel CreateViewModel() => new(
            new TaskStorageService(Path.Combine(Path.Combine(AppContext.BaseDirectory, "test-data"), "ClockTests-" + Guid.NewGuid(), "tasks.json")),
            new SilentAudio(), startTimer: false);

        public static async Task Reminder_Uses_Supplied_Time_And_Exact_Threshold()
        {
            var vm = CreateViewModel();
            await vm.Initialization;
            var now = new DateTime(2040, 1, 1, 12, 0, 0);
            vm.Tasks.Add(new TaskItem { Deadline = now.AddDays(7).AddSeconds(1) });
            Assert.Equal(0, await vm.CheckDailyRemindersAsync(now));
            Assert.Equal(1, await vm.CheckDailyRemindersAsync(now.AddSeconds(1)));
            Assert.Equal(0, await vm.CheckDailyRemindersAsync(now.AddSeconds(2)));
            vm.Cleanup();
        }

        public static async Task Concurrent_Reminder_Checks_Do_Not_Duplicate()
        {
            var vm = CreateViewModel();
            await vm.Initialization;
            vm.Tasks.Add(new TaskItem { Deadline = DateTime.Now.AddDays(1) });
            var results = await Task.WhenAll(vm.CheckDailyRemindersAsync(DateTime.Now), vm.CheckDailyRemindersAsync(DateTime.Now));
            Assert.Equal(1, results.Sum());
            vm.Cleanup();
        }

        public static async Task Storage_RoundTrip_And_Backup()
        {
            var path = Path.Combine(Path.Combine(AppContext.BaseDirectory, "test-data"), "ClockTests-" + Guid.NewGuid(), "tasks.json");
            var storage = new TaskStorageService(path);
            Assert.Equal(0, (await storage.LoadTasksAsync()).Count);
            var task = new TaskItem { Title = "Roundtrip", IsCompleted = true, LastReminderDate = DateTime.Today };
            await storage.SaveTasksAsync(new[] { task });
            await storage.SaveTasksAsync(new[] { task });
            var loaded = (await storage.LoadTasksAsync()).Single();
            Assert.Equal(task.Id, loaded.Id);
            Assert.True(loaded.IsCompleted);
            Assert.Equal(task.LastReminderDate, loaded.LastReminderDate);
            Assert.True(File.Exists(path + ".bak"));
        }

        public static async Task Corrupt_Storage_Is_Preserved_And_Reported()
        {
            var path = Path.Combine(Path.Combine(AppContext.BaseDirectory, "test-data"), "ClockTests-" + Guid.NewGuid(), "tasks.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, "broken json");
            var vm = new MainViewModel(new TaskStorageService(path), new SilentAudio(), false);
            await vm.Initialization;
            Assert.True(vm.StorageError.Contains("Could not load"));
            Assert.Equal("broken json", await File.ReadAllTextAsync(path));
            vm.NewTaskTitle = "Blocked while storage is broken";
            Assert.False(vm.AddTaskCommand.CanExecute(null));
            vm.Cleanup();
        }

        public static void Preset_And_Snooze_Preserve_Seconds()
        {
            var vm = CreateViewModel();
            var before = DateTime.Now.AddMinutes(5).AddSeconds(-1);
            vm.SetPresetCommand.Execute("5");
            Assert.True(vm.NextAlarmDateTime >= before && vm.NextAlarmDateTime <= DateTime.Now.AddMinutes(5));
            before = DateTime.Now.AddMinutes(5).AddSeconds(-1);
            vm.SnoozeAlarm();
            Assert.True(vm.NextAlarmDateTime >= before && vm.NextAlarmDateTime <= DateTime.Now.AddMinutes(5));
            vm.Cleanup();
        }

        public static void Task_Hours_Remain_12Hour_In_24Hour_Clock_Mode()
        {
            var vm = CreateViewModel();
            vm.Is24HourMode = true;
            Assert.Equal(12, vm.TaskHours.Count);
            Assert.Equal(1, vm.TaskHours.First());
            Assert.Equal(12, vm.TaskHours.Last());
            vm.Cleanup();
        }

        #region 1. Audio & Waveform Generation Tests

        public static void Waveform_RIFF_Header_Is_Valid()
        {
            byte[] waveData = BeepService.GenerateWaveform(1000, 200, 0.8);
            Assert.NotNull(waveData, "Wave data must not be null");
            Assert.True(waveData.Length > 44, "Wave data must be longer than 44-byte WAV header");

            // Verify RIFF identifier
            string riff = Encoding.ASCII.GetString(waveData, 0, 4);
            Assert.Equal("RIFF", riff, "First 4 bytes must be RIFF");

            // Verify WAVE format identifier
            string wave = Encoding.ASCII.GetString(waveData, 8, 4);
            Assert.Equal("WAVE", wave, "Bytes 8-12 must be WAVE");

            // Verify fmt identifier
            string fmt = Encoding.ASCII.GetString(waveData, 12, 4);
            Assert.Equal("fmt ", fmt, "Bytes 12-16 must be fmt ");
        }

        public static void Waveform_SampleCount_Matches_Duration()
        {
            int durationMs = 250;
            int sampleRate = 44100;
            int bytesPerSample = 2; // 16-bit mono = 2 bytes
            int expectedAudioBytes = (sampleRate * durationMs / 1000) * bytesPerSample;
            int expectedTotalBytes = 44 + expectedAudioBytes;

            byte[] waveData = BeepService.GenerateWaveform(800, durationMs, 0.5);
            Assert.Equal(expectedTotalBytes, waveData.Length, "WAV buffer byte count should match sample calculation");
        }

        #endregion

        #region 2. Task Item & 7-Day Deadline Logic Tests

        public static void TaskItem_With_Deadline_Within_7_Days_Activates_Reminder()
        {
            var task = new TaskItem
            {
                Title = "Prepare Demo",
                Deadline = DateTime.Now.AddDays(5),
                IsCompleted = false
            };

            Assert.True(task.IsReminderActive, "Task due in 5 days must have IsReminderActive == true");
            Assert.False(task.IsOverdue, "Task due in 5 days must not be overdue");
            Assert.True(task.StatusSummary.Contains("Daily Reminder"), "StatusSummary must mention Daily Reminder");
        }

        public static void TaskItem_With_Deadline_Over_7_Days_Deactivates_Reminder()
        {
            var task = new TaskItem
            {
                Title = "Future Milestone",
                Deadline = DateTime.Now.AddDays(10),
                IsCompleted = false
            };

            Assert.False(task.IsReminderActive, "Task due in 10 days must NOT have IsReminderActive == true");
            Assert.False(task.IsOverdue, "Task due in 10 days must not be overdue");
            Assert.True(task.StatusSummary.Contains("Due in 10 days"), "StatusSummary must state due in 10 days");
        }

        public static void Completed_Task_Suppresses_Reminders_Even_If_Within_7_Days()
        {
            var task = new TaskItem
            {
                Title = "Completed Project",
                Deadline = DateTime.Now.AddDays(2),
                IsCompleted = true
            };

            Assert.False(task.IsReminderActive, "Completed task must never have active reminders");
            Assert.Equal("Completed", task.StatusSummary, "Completed task status must be 'Completed'");
        }

        public static void Overdue_Task_Identified_Correctly()
        {
            var task = new TaskItem
            {
                Title = "Overdue Assignment",
                Deadline = DateTime.Now.AddDays(-2),
                IsCompleted = false
            };

            Assert.True(task.IsOverdue, "Past deadline must be marked overdue");
            Assert.True(task.IsReminderActive, "Overdue uncompleted task should remain active for alert");
            Assert.True(task.StatusSummary.Contains("Overdue"), "StatusSummary must show Overdue");
        }

        #endregion

        #region 3. Daily Reminder Execution & Rollover Tests

        public static async Task Daily_Reminder_Triggers_Once_Per_Day_Per_Task()
        {
            var vm = CreateViewModel();
            await vm.Initialization;
            var testDate = new DateTime(2026, 10, 25, 9, 0, 0);

            var task = new TaskItem
            {
                Title = "Deliver Tax Filing",
                Deadline = testDate.AddDays(3),
                IsCompleted = false,
                LastReminderDate = null
            };

            vm.Tasks.Clear();
            vm.Tasks.Add(task);

            // First check of the day
            int remindersTriggered = await vm.CheckDailyRemindersAsync(testDate);
            Assert.Equal(1, remindersTriggered, "Should trigger reminder for task within 7 days with no prior reminder");
            Assert.Equal(testDate.Date, task.LastReminderDate?.Date, "LastReminderDate should be updated to today's date");
            Assert.True(vm.HasReminderAlert, "ViewModel should raise reminder alert");

            // Second check on the SAME day should not trigger again (prevents spamming!)
            remindersTriggered = await vm.CheckDailyRemindersAsync(testDate.AddHours(2));
            Assert.Equal(0, remindersTriggered, "Should not re-trigger reminder on the same calendar day");

            // Rollover to the NEXT day (24 hours later)
            var nextDay = testDate.AddDays(1);
            remindersTriggered = await vm.CheckDailyRemindersAsync(nextDay);
            Assert.Equal(1, remindersTriggered, "Should trigger daily reminder on the following day");
            Assert.Equal(nextDay.Date, task.LastReminderDate?.Date, "LastReminderDate should be updated to next day");
        }

        #endregion

        #region 4. Clock & Alarm ViewModel Tests

        public static void Alarm_12Hour_To_24Hour_Mode_Switch_Preserves_Time()
        {
            var vm = CreateViewModel();

            // Configure 11:30 PM in 12H mode
            vm.Is24HourMode = false;
            vm.SelectedHour = 11;
            vm.SelectedMinute = 30;
            vm.SelectedAmPm = "PM";

            // Switch to 24H mode
            vm.Is24HourMode = true;

            // In 24H mode, 11 PM must be 23
            Assert.Equal(23, vm.SelectedHour, "11 PM should convert to 23 in 24-hour mode");
            Assert.Equal(30, vm.SelectedMinute, "Minutes should be unchanged");

            // Switch back to 12H mode
            vm.Is24HourMode = false;
            Assert.Equal(11, vm.SelectedHour, "23 should convert back to 11 in 12-hour mode");
            Assert.Equal("PM", vm.SelectedAmPm, "AM/PM should be PM");
        }

        public static void Alarm_Preset_Command_Arms_Alarm()
        {
            var vm = CreateViewModel();
            vm.IsAlarmArmed = false;

            // Execute +5 Min preset
            vm.SetPresetCommand.Execute("5");

            Assert.True(vm.IsAlarmArmed, "Alarm must be armed after applying preset");
            Assert.True(vm.CountdownText != "--:--:--", "CountdownText must reflect active timer");
            Assert.True(vm.AlarmStatusText.Contains("Armed"), "Status must indicate alarm is armed");
        }

        public static void Alarm_Snooze_Reschedules_For_5_Minutes()
        {
            var vm = CreateViewModel();
            vm.IsAlarmRinging = true;

            vm.SnoozeAlarmCommand.Execute(null);

            Assert.False(vm.IsAlarmRinging, "Alarm ringing state must be cleared after snooze");
            Assert.True(vm.IsAlarmArmed, "Alarm must be armed again after snooze");
            Assert.True(vm.AlarmStatusText.Contains("Snoozed"), "Status text should indicate snoozed state");
        }

        #endregion
    }
}
