using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RWORM.GUI.Models
{
    public enum TaskMode { LocalFile, Url, DiscordWebhook }

    public class TaskModel : INotifyPropertyChanged
    {
        private string _status = "Ready";
        private int    _executedCount = 0;

        public Guid     Id     { get; init; } = Guid.NewGuid();
        public string   Name   { get; set;  } = "";
        public TaskMode Mode   { get; set;  } = TaskMode.LocalFile;

        
        public string   Source { get; set;  } = "";

        public string ModeLabel => Mode switch
        {
            TaskMode.LocalFile => "Local File",
            TaskMode.Url => "URL Download",
            TaskMode.DiscordWebhook => "Discord Webhook",
            _ => "Task"
        };

        public string IconPath => Mode switch
        {
            TaskMode.LocalFile => "pack://application:,,,/Resources/Assets/filemanager.png",
            TaskMode.Url => "pack://application:,,,/Resources/Assets/loader.png",
            TaskMode.DiscordWebhook => "pack://application:,,,/Resources/Assets/discord.png",
            _ => "pack://application:,,,/Resources/Assets/tasks.png"
        };

        public int ExecutedCount
        {
            get => _executedCount;
            set { _executedCount = value; OnPropertyChanged(); }
        }

        public HashSet<string> ExecutedKeys { get; } = new(StringComparer.OrdinalIgnoreCase);

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
