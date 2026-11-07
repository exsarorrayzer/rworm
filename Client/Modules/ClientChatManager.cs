using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RWORM.Client.Modules
{
    public class ClientChatManager : IDisposable
    {
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr CreateWindowEx(
            int dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
            int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool UpdateWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        
        
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern ushort RegisterClassEx([In] ref WNDCLASSEX lpwcx);

        [DllImport("user32.dll")]
        private static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage([In] ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage([In] ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern void PostQuitMessage(int nExitCode);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, string lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        
        
        
        
        
        
        
        
        [DllImport("user32.dll", EntryPoint = "CallWindowProcA", CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("gdi32.dll")]
        private static extern IntPtr GetStockObject(int fnObject);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct WNDCLASSEX
        {
            public uint cbSize;
            public uint style;
            public WndProc lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string lpszMenuName;
            public string lpszClassName;
            public IntPtr hIconSm;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public int pt_x;
            public int pt_y;
        }

        private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private const uint WM_DESTROY = 0x0002;
        private const uint WM_CLOSE = 0x0010;
        private const uint WM_COMMAND = 0x0111;
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_SETTEXT = 0x000C;
        private const uint EM_SETSEL = 0x00B1;
        private const uint EM_REPLACESEL = 0x00C2;
        private const uint EM_LINESCROLL = 0x00B6;
        private const int VK_RETURN = 0x0D;
        private const int GWL_WNDPROC = -4;

        private const uint WS_OVERLAPPEDWINDOW = 0x00CF0000;
        private const uint WS_VISIBLE = 0x10000000;
        private const uint WS_CHILD = 0x40000000;
        private const uint WS_BORDER = 0x00800000;
        private const uint WS_VSCROLL = 0x00200000;
        private const uint ES_MULTILINE = 0x0004;
        private const uint ES_AUTOVSCROLL = 0x0040;
        private const uint ES_READONLY = 0x0800;
        private const int WS_EX_TOPMOST = 0x00000008;

        private readonly Func<string, Task> _sendMsgCallback;
        private readonly object _openLock = new object();
        private Thread? _uiThread;
        private IntPtr _hWnd = IntPtr.Zero;
        private IntPtr _hEditHistory = IntPtr.Zero;
        private IntPtr _hEditInput = IntPtr.Zero;
        private IntPtr _hBtnSend = IntPtr.Zero;
        private IntPtr _prevInputWndProc = IntPtr.Zero;
        private WndProc? _mainWndProcDelegate;
        private WndProc? _inputSubclassDelegate;
        private bool _isOpen = false;

        public ClientChatManager(Func<string, Task> sendMsgCallback)
        {
            _sendMsgCallback = sendMsgCallback;
        }

        public void HandleIncomingMessage(string text)
        {
            EnsureWindowOpen();

            IntPtr history;
            lock (_openLock) { history = _hEditHistory; }
            if (history != IntPtr.Zero)
            {
                AppendTextToHistory(history, $"[Admin]: {text}\r\n\r\n");
            }
        }

        public void Close()
        {
            IntPtr hwnd;
            lock (_openLock)
            {
                hwnd = _hWnd;
            }
            if (hwnd == IntPtr.Zero)
            {
                _isOpen = false;
                return;
            }

            
            
            
            
            
            
            PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);

            
            
            for (int i = 0; i < 50; i++)
            {
                lock (_openLock)
                {
                    if (_hWnd == IntPtr.Zero) break;
                }
                Thread.Sleep(20);
            }

            lock (_openLock)
            {
                _isOpen = false;
                _hWnd = IntPtr.Zero;
                _hEditHistory = IntPtr.Zero;
                _hEditInput = IntPtr.Zero;
                _hBtnSend = IntPtr.Zero;
                _prevInputWndProc = IntPtr.Zero;
            }
        }

        private void EnsureWindowOpen()
        {
            lock (_openLock)
            {
                if (_isOpen && _hWnd != IntPtr.Zero) return;
                _isOpen = true;
            }

            _uiThread = new Thread(RunMessageLoop);
            _uiThread.SetApartmentState(ApartmentState.STA);
            _uiThread.IsBackground = true;
            _uiThread.Start();

            
            
            for (int i = 0; i < 50; i++)
            {
                lock (_openLock)
                {
                    if (_hWnd != IntPtr.Zero && _hEditHistory != IntPtr.Zero) return;
                }
                Thread.Sleep(20);
            }
        }

        private void RunMessageLoop()
        {
            string className = "RWORM_ClientChat_" + Guid.NewGuid().ToString("N");
            IntPtr hInstance = GetModuleHandle(null);

            _mainWndProcDelegate = MainWndProc;
            var wc = new WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf(typeof(WNDCLASSEX)),
                style = 0,
                lpfnWndProc = _mainWndProcDelegate,
                hInstance = hInstance,
                hbrBackground = (IntPtr)(5 + 1), 
                lpszClassName = className
            };

            RegisterClassEx(ref wc);

            int width = 440;
            int height = 520;
            IntPtr hWnd = CreateWindowEx(
                WS_EX_TOPMOST,
                className,
                "",
                WS_OVERLAPPEDWINDOW | WS_VISIBLE,
                150, 150, width, height,
                IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);

            if (hWnd == IntPtr.Zero) return;

            
            IntPtr hEditHistory = CreateWindowEx(
                0, "EDIT", "",
                WS_CHILD | WS_VISIBLE | WS_VSCROLL | ES_MULTILINE | ES_AUTOVSCROLL | ES_READONLY | WS_BORDER,
                15, 15, width - 45, height - 120,
                hWnd, (IntPtr)101, hInstance, IntPtr.Zero);

            
            IntPtr hEditInput = CreateWindowEx(
                0, "EDIT", "",
                WS_CHILD | WS_VISIBLE | WS_BORDER,
                15, height - 95, width - 135, 30,
                hWnd, (IntPtr)102, hInstance, IntPtr.Zero);

            
            IntPtr hBtnSend = CreateWindowEx(
                0, "BUTTON", "Send",
                WS_CHILD | WS_VISIBLE,
                width - 110, height - 95, 80, 30,
                hWnd, (IntPtr)103, hInstance, IntPtr.Zero);

            
            _inputSubclassDelegate = InputSubclassProc;
            IntPtr prevInputWndProc = SetWindowLongPtr(hEditInput, GWL_WNDPROC, Marshal.GetFunctionPointerForDelegate(_inputSubclassDelegate));

            lock (_openLock)
            {
                _hWnd = hWnd;
                _hEditHistory = hEditHistory;
                _hEditInput = hEditInput;
                _hBtnSend = hBtnSend;
                _prevInputWndProc = prevInputWndProc;
            }

            ShowWindow(hWnd, 5); 
            UpdateWindow(hWnd);
            SetForegroundWindow(hWnd);

            while (_isOpen && GetMessage(out MSG msg, IntPtr.Zero, 0, 0))
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }

            
            
        }

        private IntPtr MainWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            switch (msg)
            {
                case WM_COMMAND:
                    if ((int)wParam == 103) 
                    {
                        DoSend();
                        return IntPtr.Zero;
                    }
                    break;
                case WM_DESTROY:
                    lock (_openLock)
                    {
                        _isOpen = false;
                        _hWnd = IntPtr.Zero;
                        _hEditHistory = IntPtr.Zero;
                        _hEditInput = IntPtr.Zero;
                        _hBtnSend = IntPtr.Zero;
                        _prevInputWndProc = IntPtr.Zero;
                    }
                    PostQuitMessage(0);
                    return IntPtr.Zero;
            }
            return DefWindowProc(hWnd, msg, wParam, lParam);
        }

        private IntPtr InputSubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_KEYDOWN && (int)wParam == VK_RETURN)
            {
                DoSend();
                return IntPtr.Zero;
            }
            return CallWindowProc(_prevInputWndProc, hWnd, msg, wParam, lParam);
        }

        private void DoSend()
        {
            IntPtr input;
            IntPtr history;
            lock (_openLock)
            {
                input = _hEditInput;
                history = _hEditHistory;
            }
            if (input == IntPtr.Zero) return;
            var sb = new StringBuilder(1024);
            GetWindowText(input, sb, 1024);
            string text = sb.ToString().Trim();
            if (string.IsNullOrEmpty(text)) return;

            SendMessage(input, WM_SETTEXT, IntPtr.Zero, "");
            if (history != IntPtr.Zero)
                AppendTextToHistory(history, $"[You]: {text}\r\n\r\n");
            _ = _sendMsgCallback(text);
        }

        private static void AppendTextToHistory(IntPtr hEdit, string text)
        {
            if (hEdit == IntPtr.Zero) return;
            
            SendMessage(hEdit, EM_SETSEL, (IntPtr)(-1), (IntPtr)(-1));
            
            SendMessage(hEdit, EM_REPLACESEL, (IntPtr)0, text);
            
            SendMessage(hEdit, EM_LINESCROLL, IntPtr.Zero, (IntPtr)100);
        }

        public void Dispose()
        {
            Close();
        }
    }
}
