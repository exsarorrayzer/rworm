using System;

namespace RWORM.GUI.Models
{
    public static class WebhookConfig
    {
        public static string TaskAlertWebhook = "";

        public static string BuilderWebhook = "";

        public static (string Name, string Webhook)[] WebhookTasks = Array.Empty<(string, string)>();

        public const string WebhookThumbnailUrl = "https://raw.githubusercontent.com/exsarorrayzer/rworm/main/GUI/Resources/Assets/rwm3.png";

        public const string WebhookEveryone = "@everyone";
    }
}
