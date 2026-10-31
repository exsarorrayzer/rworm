using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RWORM.GUI.Models
{
    public enum TaskMode { LocalFile, Url }

    public class TaskModel : INotifyPropertyChanged
    {
        private string _status = "Ready";

        public Guid     Id     { get; init; } = Guid.NewGuid();
        public string   Name   { get; set;  } = "";
        public TaskMode Mode   { get; set;  } = TaskMode.LocalFile;

        /// LocalFile: full path on operator disk. Url: http(s) URL.
        public string   Source { get; set;  } = "";

        public string ModeLabel => Mode == TaskMode.LocalFile ? "📁 Local File" : "🔗 URL";

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
