using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using WpfDigitalClockAlarm.Commands;
using WpfDigitalClockAlarm.Models;
using WpfDigitalClockAlarm.Services;

namespace WpfDigitalClockAlarm.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly DispatcherTimer _clockTimer;
        private readonly BeepService _beepService;
        private readonly TaskStorageService _taskStorageService;

        // Clock State
        private string _hours = "12";
        private string _minutes = "00";
        private string _seconds = "00";
        private string _amPm = "AM";
        private string _dateString = string.Empty;
        private bool _is24HourMode = false;
        private bool _colonVisible = true;
        private DateTime _lastReminderCheck = DateTime.MinValue;
        private bool _checkingReminders;
        private bool _tasksLoaded;
        private string _storageError = string.Empty;
        public string StorageError { get => _storageError; private set => SetProperty(ref _storageError, value); }
        public ObservableCollection<int> TaskHours { get; } = new(Enumerable.Range(1, 12));
        public Task Initialization { get; }
        public DateTime? NextAlarmDateTime => _nextAlarmDateTime;

        // Alarm Settings
        private int _selectedHour = 12;
        private int _selectedMinute = 0;
        private int _selectedSecond = 0;
        private string _selectedAmPm = "PM";
        private string _alarmLabel = "Wake up & Conquer!";
        private AlarmSoundType _selectedSound = AlarmSoundType.ClassicDigital;

        // Alarm State
        private bool _isAlarmArmed = false;
        private bool _isAlarmRinging = false;
        private string _alarmStatusText = "Alarm is currently disarmed";
        private string _countdownText = "--:--:--";
        private DateTime? _nextAlarmDateTime;

        // Task & Reminder State
        private string _newTaskTitle = string.Empty;
        private string _newTaskDescription = string.Empty;
        private TaskPriority _newTaskPriority = TaskPriority.High;
        private DateTime _newTaskDueDate = DateTime.Today.AddDays(7);
        private int _newTaskDueHour = 5;
        private int _newTaskDueMinute = 0;
        private string _newTaskDueAmPm = "PM";
        private string _taskFilter = "All"; // All, Active, Reminders, Completed

        private bool _hasReminderAlert = false;
        private string _reminderAlertTitle = string.Empty;
        private string _reminderAlertDetails = string.Empty;
        private int _selectedMainTab = 0; // 0 = Alarm, 1 = Tasks

        public ObservableCollection<TaskItem> Tasks { get; } = new();
        public ICollectionView FilteredTasks { get; }

        // Presets & Sound Options
        public ObservableCollection<int> AvailableHours { get; } = new();
        public ObservableCollection<int> AvailableMinutes { get; } = new();
        public ObservableCollection<int> AvailableSeconds { get; } = new();
        public ObservableCollection<string> AvailableAmPm { get; } = new() { "AM", "PM" };
        public ObservableCollection<TaskPriority> AvailablePriorities { get; } = new()
        {
            TaskPriority.Urgent,
            TaskPriority.High,
            TaskPriority.Medium,
            TaskPriority.Low
        };

        public ObservableCollection<AlarmSoundType> SoundTypes { get; } = new()
        {
            AlarmSoundType.ClassicDigital,
            AlarmSoundType.HighAlert,
            AlarmSoundType.FastChirp
        };

        // Commands
        public ICommand ToggleAlarmArmCommand { get; }
        public ICommand DismissAlarmCommand { get; }
        public ICommand SnoozeAlarmCommand { get; }
        public ICommand TestBeepCommand { get; }
        public ICommand SetPresetCommand { get; }
        public ICommand ToggleTimeFormatCommand { get; }

        public ICommand AddTaskCommand { get; }
        public ICommand DeleteTaskCommand { get; }
        public ICommand ToggleTaskCompleteCommand { get; }
        public ICommand SetTaskFilterCommand { get; }
        public ICommand DismissReminderAlertCommand { get; }
        public ICommand TestReminderChimeCommand { get; }
        public ICommand SwitchTabCommand { get; }

        public MainViewModel() : this(new TaskStorageService(), new BeepService()) { }

        public MainViewModel(TaskStorageService storage, BeepService audio, bool startTimer = true)
        {
            _beepService = audio;
            _taskStorageService = storage;

            FilteredTasks = CollectionViewSource.GetDefaultView(Tasks);
            FilteredTasks.Filter = FilterTaskItem;

            ToggleAlarmArmCommand = new RelayCommand(ToggleAlarmArm);
            DismissAlarmCommand = new RelayCommand(DismissAlarm);
            SnoozeAlarmCommand = new RelayCommand(SnoozeAlarm);
            TestBeepCommand = new RelayCommand(async () => await TestBeep());
            SetPresetCommand = new RelayCommand(param => SetPreset(param));
            ToggleTimeFormatCommand = new RelayCommand(() => Is24HourMode = !Is24HourMode);

            AddTaskCommand = new RelayCommand(async () => await AddTaskAsync(), () => _tasksLoaded && !string.IsNullOrWhiteSpace(NewTaskTitle));
            DeleteTaskCommand = new RelayCommand(async param => await DeleteTaskAsync(param));
            ToggleTaskCompleteCommand = new RelayCommand(async param => await ToggleTaskCompleteAsync(param));
            SetTaskFilterCommand = new RelayCommand(param => SetTaskFilter(param?.ToString()));
            DismissReminderAlertCommand = new RelayCommand(() => HasReminderAlert = false);
            TestReminderChimeCommand = new RelayCommand(async () => await _beepService.PlayReminderChimeAsync());
            SwitchTabCommand = new RelayCommand(param =>
            {
                if (int.TryParse(param?.ToString(), out int tab))
                    SelectedMainTab = tab;
            });

            // Populate hour & minute options
            PopulateHourOptions();
            for (int i = 0; i < 60; i++)
            {
                AvailableMinutes.Add(i);
                AvailableSeconds.Add(i);
            }

            // Default alarm time: 5 minutes from now
            var defaultTime = DateTime.Now.AddMinutes(5);
            InitializeAlarmTime(defaultTime);

            // High-frequency UI timer for smooth clock updates and colon blink
            _clockTimer = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _clockTimer.Tick += OnClockTick;
            if (startTimer) _clockTimer.Start();

            UpdateClock(DateTime.Now);

            // Load saved tasks asynchronously
            Initialization = LoadTasksAndCheckRemindersAsync();
        }

        #region Clock Properties

        public string Hours
        {
            get => _hours;
            set => SetProperty(ref _hours, value);
        }

        public string Minutes
        {
            get => _minutes;
            set => SetProperty(ref _minutes, value);
        }

        public string Seconds
        {
            get => _seconds;
            set => SetProperty(ref _seconds, value);
        }

        public string AmPm
        {
            get => _amPm;
            set => SetProperty(ref _amPm, value);
        }

        public string DateString
        {
            get => _dateString;
            set => SetProperty(ref _dateString, value);
        }

        public bool Is24HourMode
        {
            get => _is24HourMode;
            set
            {
                if (SetProperty(ref _is24HourMode, value))
                {
                    if (value)
                    {
                        int h24 = _selectedAmPm == "AM"
                            ? (_selectedHour == 12 ? 0 : _selectedHour)
                            : (_selectedHour == 12 ? 12 : _selectedHour + 12);
                        PopulateHourOptions();
                        SelectedHour = h24;
                    }
                    else
                    {
                        int h12 = _selectedHour switch
                        {
                            0 => 12,
                            > 12 => _selectedHour - 12,
                            _ => _selectedHour
                        };
                        SelectedAmPm = _selectedHour >= 12 ? "PM" : "AM";
                        PopulateHourOptions();
                        SelectedHour = h12;
                    }

                    UpdateClock(DateTime.Now);
                    RecalculateAlarmDateTime();
                    OnPropertyChanged(nameof(ShowAmPm));
                }
            }
        }

        public bool ShowAmPm => !Is24HourMode;

        public bool ColonVisible
        {
            get => _colonVisible;
            set => SetProperty(ref _colonVisible, value);
        }

        #endregion

        #region Alarm Configuration Properties

        public int SelectedHour
        {
            get => _selectedHour;
            set
            {
                if (SetProperty(ref _selectedHour, value))
                {
                    RecalculateAlarmDateTime();
                }
            }
        }

        public int SelectedMinute
        {
            get => _selectedMinute;
            set
            {
                if (SetProperty(ref _selectedMinute, value))
                {
                    RecalculateAlarmDateTime();
                }
            }
        }

        public int SelectedSecond
        {
            get => _selectedSecond;
            set
            {
                if (SetProperty(ref _selectedSecond, value))
                {
                    RecalculateAlarmDateTime();
                }
            }
        }

        public string SelectedAmPm
        {
            get => _selectedAmPm;
            set
            {
                if (SetProperty(ref _selectedAmPm, value))
                {
                    RecalculateAlarmDateTime();
                }
            }
        }

        public string AlarmLabel
        {
            get => _alarmLabel;
            set => SetProperty(ref _alarmLabel, value);
        }

        public AlarmSoundType SelectedSound
        {
            get => _selectedSound;
            set => SetProperty(ref _selectedSound, value);
        }

        public bool IsAlarmArmed
        {
            get => _isAlarmArmed;
            set
            {
                if (SetProperty(ref _isAlarmArmed, value))
                {
                    if (value)
                    {
                        RecalculateAlarmDateTime();
                    }
                    else
                    {
                        _nextAlarmDateTime = null;
                        AlarmStatusText = "Alarm is disarmed";
                        CountdownText = "--:--:--";
                    }
                    OnPropertyChanged(nameof(ArmButtonText));
                }
            }
        }

        public string ArmButtonText => IsAlarmArmed ? "DISARM ALARM" : "ARM ALARM";

        public bool IsAlarmRinging
        {
            get => _isAlarmRinging;
            set => SetProperty(ref _isAlarmRinging, value);
        }

        public string AlarmStatusText
        {
            get => _alarmStatusText;
            set => SetProperty(ref _alarmStatusText, value);
        }

        public string CountdownText
        {
            get => _countdownText;
            set => SetProperty(ref _countdownText, value);
        }

        #endregion

        #region Tasks & Reminders Properties

        public int SelectedMainTab
        {
            get => _selectedMainTab;
            set => SetProperty(ref _selectedMainTab, value);
        }

        public string NewTaskTitle
        {
            get => _newTaskTitle;
            set => SetProperty(ref _newTaskTitle, value);
        }

        public string NewTaskDescription
        {
            get => _newTaskDescription;
            set => SetProperty(ref _newTaskDescription, value);
        }

        public TaskPriority NewTaskPriority
        {
            get => _newTaskPriority;
            set => SetProperty(ref _newTaskPriority, value);
        }

        public DateTime NewTaskDueDate
        {
            get => _newTaskDueDate;
            set => SetProperty(ref _newTaskDueDate, value);
        }

        public int NewTaskDueHour
        {
            get => _newTaskDueHour;
            set => SetProperty(ref _newTaskDueHour, value);
        }

        public int NewTaskDueMinute
        {
            get => _newTaskDueMinute;
            set => SetProperty(ref _newTaskDueMinute, value);
        }

        public string NewTaskDueAmPm
        {
            get => _newTaskDueAmPm;
            set => SetProperty(ref _newTaskDueAmPm, value);
        }

        public string TaskFilter
        {
            get => _taskFilter;
            set
            {
                if (SetProperty(ref _taskFilter, value))
                {
                    FilteredTasks.Refresh();
                }
            }
        }

        public bool HasReminderAlert
        {
            get => _hasReminderAlert;
            set => SetProperty(ref _hasReminderAlert, value);
        }

        public string ReminderAlertTitle
        {
            get => _reminderAlertTitle;
            set => SetProperty(ref _reminderAlertTitle, value);
        }

        public string ReminderAlertDetails
        {
            get => _reminderAlertDetails;
            set => SetProperty(ref _reminderAlertDetails, value);
        }

        public int ActiveRemindersCount => Tasks.Count(t => t.IsReminderActive);
        public int TotalTasksCount => Tasks.Count;

        #endregion

        #region Task Logic & Reminders (Daily starting with 7 days to go)

        private async Task LoadTasksAndCheckRemindersAsync()
        {
            System.Collections.Generic.List<TaskItem> loaded;
            try { loaded = await _taskStorageService.LoadTasksAsync(); }
            catch (Exception ex) { StorageError = $"Could not load tasks: {ex.Message}. Your file has been preserved; restart after fixing it."; return; }
            Tasks.Clear();
            foreach (var t in loaded)
            {
                Tasks.Add(t);
            }
            FilteredTasks.Refresh();
            UpdateTaskCounts();

            _tasksLoaded = true;
            CommandManager.InvalidateRequerySuggested();
            // Run daily reminder check on launch
            await CheckDailyRemindersAsync(DateTime.Now);
        }

        /// <summary>
        /// Checks for tasks whose deadline is within 7 days.
        /// Fires a daily reminder beep and alert banner once per day per task.
        /// </summary>
        public async Task<int> CheckDailyRemindersAsync(DateTime now)
        {
            if (!_tasksLoaded || _checkingReminders) return 0;
            _checkingReminders = true;
            try
            {
            var today = now.Date;
            var dueForReminder = Tasks.Where(t =>
                !t.IsCompleted &&
                t.Deadline - now <= TimeSpan.FromDays(7) &&
                (t.LastReminderDate == null || t.LastReminderDate.Value.Date < today)
            ).ToList();

            if (dueForReminder.Count > 0)
            {
                // Mark today's reminder as delivered
                foreach (var task in dueForReminder)
                {
                    task.LastReminderDate = today;
                }

                // Show visual alert toast/banner
                ReminderAlertTitle = dueForReminder.Count == 1
                    ? $"🔔 Daily Reminder: '{dueForReminder[0].Title}'"
                    : $"🔔 Daily Reminders: {dueForReminder.Count} tasks due within 7 days!";

                ReminderAlertDetails = string.Join(" • ", dueForReminder.Select(t =>
                    $"{t.Title} (Priority: {t.Priority}, {t.StatusSummary})"));

                HasReminderAlert = true;
                await SaveTasksAsync();
                await _beepService.PlayReminderChimeAsync();
            }

            _lastReminderCheck = now;
            UpdateTaskCounts();
            return dueForReminder.Count;
            }
            finally { _checkingReminders = false; }
        }

        private async Task SaveTasksAsync()
        {
            if (!_tasksLoaded) return;
            try { await _taskStorageService.SaveTasksAsync(Tasks); StorageError = string.Empty; }
            catch (Exception ex) { StorageError = $"Could not save tasks: {ex.Message}. Keep the app open and retry a task action."; }
        }

        private async Task AddTaskAsync()
        {
            if (!_tasksLoaded || string.IsNullOrWhiteSpace(NewTaskTitle))
                return;

            int hour24 = NewTaskDueAmPm == "AM"
                ? (NewTaskDueHour == 12 ? 0 : NewTaskDueHour)
                : (NewTaskDueHour == 12 ? 12 : NewTaskDueHour + 12);

            var deadline = new DateTime(
                NewTaskDueDate.Year,
                NewTaskDueDate.Month,
                NewTaskDueDate.Day,
                hour24,
                NewTaskDueMinute,
                0);

            var task = new TaskItem
            {
                Title = NewTaskTitle.Trim(),
                Description = NewTaskDescription?.Trim() ?? string.Empty,
                Priority = NewTaskPriority,
                Deadline = deadline,
                IsCompleted = false
            };

            Tasks.Insert(0, task);
            NewTaskTitle = string.Empty;
            NewTaskDescription = string.Empty;

            await SaveTasksAsync();
            FilteredTasks.Refresh();
            UpdateTaskCounts();

            // If task is within 7 days, trigger daily reminder immediately
            await CheckDailyRemindersAsync(DateTime.Now);
        }

        private async Task DeleteTaskAsync(object? param)
        {
            if (param is TaskItem task && Tasks.Contains(task))
            {
                Tasks.Remove(task);
                await SaveTasksAsync();
                FilteredTasks.Refresh();
                UpdateTaskCounts();
            }
        }

        private async Task ToggleTaskCompleteAsync(object? param)
        {
            if (param is TaskItem task)
            {
                task.IsCompleted = !task.IsCompleted;
                task.RefreshComputedProperties();
                await SaveTasksAsync();
                FilteredTasks.Refresh();
                UpdateTaskCounts();
            }
        }

        private void SetTaskFilter(string? filter)
        {
            TaskFilter = filter ?? "All";
        }

        private bool FilterTaskItem(object obj)
        {
            if (obj is not TaskItem task) return false;

            return TaskFilter switch
            {
                "Active" => !task.IsCompleted,
                "Reminders" => task.IsReminderActive,
                "Completed" => task.IsCompleted,
                _ => true // "All"
            };
        }

        private void UpdateTaskCounts()
        {
            OnPropertyChanged(nameof(ActiveRemindersCount));
            OnPropertyChanged(nameof(TotalTasksCount));
        }

        #endregion

        #region Private Helpers & Event Handlers

        private void PopulateHourOptions()
        {
            int currentSelection = _selectedHour;
            AvailableHours.Clear();

            if (Is24HourMode)
            {
                for (int i = 0; i < 24; i++)
                    AvailableHours.Add(i);

                if (!AvailableHours.Contains(currentSelection))
                    SelectedHour = 12;
            }
            else
            {
                for (int i = 1; i <= 12; i++)
                    AvailableHours.Add(i);

                if (currentSelection < 1 || currentSelection > 12)
                    SelectedHour = (currentSelection == 0 ? 12 : (currentSelection % 12));
            }
        }

        private void InitializeAlarmTime(DateTime target)
        {
            if (Is24HourMode)
            {
                SelectedHour = target.Hour;
            }
            else
            {
                SelectedHour = target.Hour switch
                {
                    0 => 12,
                    > 12 => target.Hour - 12,
                    _ => target.Hour
                };
                SelectedAmPm = target.Hour >= 12 ? "PM" : "AM";
            }

            SelectedMinute = target.Minute;
            SelectedSecond = target.Second;
            RecalculateAlarmDateTime();
        }

        private void OnClockTick(object? sender, EventArgs e)
        {
            var now = DateTime.Now;
            UpdateClock(now);

            // Blink separator colons every half second
            ColonVisible = (now.Millisecond < 500);

            // Check Alarm condition if armed
            if (IsAlarmArmed && _nextAlarmDateTime.HasValue)
            {
                var remaining = _nextAlarmDateTime.Value - now;

                if (remaining.TotalSeconds <= 0)
                {
                    TriggerAlarm();
                }
                else
                {
                    CountdownText = string.Format("{0:D2}:{1:D2}:{2:D2}",
                        (int)remaining.TotalHours,
                        remaining.Minutes,
                        remaining.Seconds);

                    AlarmStatusText = $"Alarm set for {_nextAlarmDateTime.Value:t} (rings in {(int)remaining.TotalMinutes}m {remaining.Seconds}s)";
                }
            }

            // Periodic task status refresh & daily reminder rollover check
            if (_tasksLoaded && now - _lastReminderCheck >= TimeSpan.FromSeconds(1))
            {
                foreach (var task in Tasks) task.RefreshComputedProperties();
                FilteredTasks.Refresh();
                UpdateTaskCounts();
                _ = CheckDailyRemindersAsync(now);
            }
        }

        private void UpdateClock(DateTime now)
        {
            if (Is24HourMode)
            {
                Hours = now.ToString("HH");
                AmPm = string.Empty;
            }
            else
            {
                Hours = now.ToString("hh");
                AmPm = now.ToString("tt").ToUpperInvariant();
            }

            Minutes = now.ToString("mm");
            Seconds = now.ToString("ss");
            DateString = now.ToString("dddd, MMMM dd, yyyy");
        }

        private void RecalculateAlarmDateTime()
        {
            int hour24;
            if (Is24HourMode)
            {
                hour24 = SelectedHour;
            }
            else
            {
                string amPm = string.IsNullOrEmpty(SelectedAmPm) ? "AM" : SelectedAmPm;
                if (amPm == "AM")
                {
                    hour24 = SelectedHour == 12 ? 0 : SelectedHour;
                }
                else
                {
                    hour24 = SelectedHour == 12 ? 12 : SelectedHour + 12;
                }
            }

            var now = DateTime.Now;
            var target = new DateTime(now.Year, now.Month, now.Day, hour24, SelectedMinute, SelectedSecond);

            if (target <= now)
            {
                target = target.AddDays(1);
            }

            _nextAlarmDateTime = target;

            if (IsAlarmArmed)
            {
                var remaining = target - now;
                CountdownText = string.Format("{0:D2}:{1:D2}:{2:D2}",
                    (int)remaining.TotalHours,
                    remaining.Minutes,
                    remaining.Seconds);
                AlarmStatusText = $"Armed for {target:t} (in {(int)remaining.TotalMinutes}m {remaining.Seconds}s)";
            }
        }

        private void ToggleAlarmArm()
        {
            IsAlarmArmed = !IsAlarmArmed;
        }

        private void TriggerAlarm()
        {
            IsAlarmArmed = false;
            IsAlarmRinging = true;
            AlarmStatusText = $"ALARM TRIGGERED: {AlarmLabel}";
            CountdownText = "RINGING!";

            _beepService.StartAlarmLoop(SelectedSound);
        }

        public void DismissAlarm()
        {
            _beepService.StopAlarm();
            IsAlarmRinging = false;
            AlarmStatusText = "Alarm dismissed";
            CountdownText = "--:--:--";
        }

        public void SnoozeAlarm()
        {
            DismissAlarm();
            var snoozeTarget = DateTime.Now.AddMinutes(5);
            InitializeAlarmTime(snoozeTarget);
            IsAlarmArmed = true;
            AlarmStatusText = $"Snoozed! Next alarm at {snoozeTarget:t}";
        }

        private async Task TestBeep()
        {
            await _beepService.PlayTestBeepAsync();
        }

        private void SetPreset(object? param)
        {
            if (param != null && int.TryParse(param.ToString(), out int minutes))
            {
                var target = DateTime.Now.AddMinutes(minutes);
                InitializeAlarmTime(target);
                IsAlarmArmed = true;
            }
        }

        public void Cleanup()
        {
            _clockTimer.Stop();
            _beepService.StopAlarm();
        }

        #endregion
    }
}
