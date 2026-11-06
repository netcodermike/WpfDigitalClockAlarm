using System;
using System.Text.Json.Serialization;
using WpfDigitalClockAlarm.ViewModels;

namespace WpfDigitalClockAlarm.Models
{
    public class TaskItem : ViewModelBase
    {
        private Guid _id = Guid.NewGuid();
        private string _title = string.Empty;
        private string _description = string.Empty;
        private TaskPriority _priority = TaskPriority.Medium;
        private DateTime _deadline = DateTime.Now.AddDays(7);
        private bool _isCompleted = false;
        private DateTime? _lastReminderDate;

        public Guid Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public TaskPriority Priority
        {
            get => _priority;
            set => SetProperty(ref _priority, value);
        }

        public DateTime Deadline
        {
            get => _deadline;
            set
            {
                if (SetProperty(ref _deadline, value))
                {
                    RefreshComputedProperties();
                }
            }
        }

        public bool IsCompleted
        {
            get => _isCompleted;
            set
            {
                if (SetProperty(ref _isCompleted, value))
                {
                    RefreshComputedProperties();
                }
            }
        }

        /// <summary>
        /// Date when daily reminder was last triggered.
        /// </summary>
        public DateTime? LastReminderDate
        {
            get => _lastReminderDate;
            set => SetProperty(ref _lastReminderDate, value);
        }

        [JsonIgnore]
        public double DaysRemaining => (Deadline - DateTime.Now).TotalDays;

        [JsonIgnore]
        public bool IsReminderActive => !IsCompleted && DaysRemaining <= 7.0;

        [JsonIgnore]
        public bool IsOverdue => !IsCompleted && DaysRemaining < 0;

        [JsonIgnore]
        public string StatusSummary
        {
            get
            {
                if (IsCompleted) return "Completed";

                var remaining = Deadline - DateTime.Now;
                if (remaining.TotalSeconds <= 0)
                {
                    int overdueDays = Math.Abs((int)remaining.TotalDays);
                    return overdueDays == 0 ? "⚠️ Overdue today!" : $"🚨 Overdue by {overdueDays}d";
                }

                if (remaining.TotalDays <= 1.0)
                {
                    return $"⚡ Due today at {Deadline:t} ({(int)remaining.TotalHours}h remaining)";
                }

                int daysLeft = (int)Math.Ceiling(remaining.TotalDays);
                if (daysLeft <= 7)
                {
                    return $"🔔 Daily Reminder: {daysLeft} days left (due {Deadline:MMM dd})";
                }

                return $"📅 Due in {daysLeft} days ({Deadline:MMM dd, yyyy})";
            }
        }

        public void RefreshComputedProperties()
        {
            OnPropertyChanged(nameof(DaysRemaining));
            OnPropertyChanged(nameof(StatusSummary));
            OnPropertyChanged(nameof(IsReminderActive));
            OnPropertyChanged(nameof(IsOverdue));
        }
    }
}
