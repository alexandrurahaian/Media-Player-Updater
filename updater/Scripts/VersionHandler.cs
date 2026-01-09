using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;

namespace updater.Scripts
{
    public static class VersionHandler
    {
        private static readonly string? CURRENT_APP_VERSION = GetCurrentVersion();
        private static MainWindow mainWindow;

        private static HttpClient client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15),
        };

        public static void InitClient(MainWindow mw)
        {
            try
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Media Player Application");
                mainWindow = mw;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private static bool MakeBackup()
        {
            int randId = new Random().Next(999999);
            string dataFile = Path.Combine(AppContext.BaseDirectory, "media.db");
            string backupFolder = Path.Combine(AppContext.BaseDirectory, "Backups");
            string dataBakFile = Path.Combine(backupFolder, $"bak_media-{randId}.db");
            
            try
            {
                if (!Directory.Exists(backupFolder)) Directory.CreateDirectory(backupFolder);
                File.Copy(dataFile, dataBakFile, false);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not create backup: {ex.Message}\n{ex.StackTrace}\n{ex.InnerException}");
                return false;
            }
        }

        private static async void DownloadUpdate()
        {
            string download_url = "https://github.com/vortex3225/media_player/releases/latest/download/media_player.zip";
            string fileToWrite = Path.Combine(AppContext.BaseDirectory, "media_player.zip");
            string newFolder = Path.Combine(AppContext.BaseDirectory, "media_temp");
            string tempOldFolder = Path.Combine(AppContext.BaseDirectory, "Old");

            try
            {
                bool couldMakeBak = MakeBackup();
                if (!couldMakeBak)
                {
                    MessageBox.Show($"Updating cancelled. Could not make data backup...");
                    Environment.Exit(0);
                    return;
                }

                if (Directory.Exists(tempOldFolder))
                {
                    Directory.Delete(tempOldFolder, true);
                    Directory.CreateDirectory(tempOldFolder);
                }
                else Directory.CreateDirectory(tempOldFolder);

                using (var s = await client.GetStreamAsync(new Uri(download_url)))
                {
                    using (var fs = new FileStream(fileToWrite, FileMode.CreateNew))
                    {
                        await s.CopyToAsync(fs);
                    }
                }

                System.IO.Compression.ZipFile.ExtractToDirectory(fileToWrite, newFolder);
                File.Delete(fileToWrite);

                string[] new_files = Directory.GetFiles(newFolder, "*", SearchOption.TopDirectoryOnly)
                                              .ToArray();
                string[] old_files = Directory.GetFiles(AppContext.BaseDirectory);

                string[] persistent_files = { "media.db", "Backups", "auto_update.cfg", "updater.exe", "app_config.json" };

                Debug.WriteLine("Old files: ");
                foreach (string filePath in old_files)
                {
                    Debug.WriteLine(filePath);
                    string fileName = Path.GetFileName(filePath);
                    if (persistent_files.Any(p =>
                        string.Equals(p, fileName, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    File.Move(filePath, Path.Combine(tempOldFolder, fileName));
                }

                Debug.WriteLine("New files: ");
                foreach (string filePath in new_files)
                {
                    Debug.WriteLine(filePath);
                    string fileName = Path.GetFileName(filePath);
                    
                    if (persistent_files.Any(p =>
                        string.Equals(p, fileName, StringComparison.OrdinalIgnoreCase)))
                    {
                        File.Delete(filePath);
                        continue;
                    }

                    File.Copy(filePath, Path.Combine(AppContext.BaseDirectory, fileName), true);
                }
                Process media_player_proc = new Process();
                media_player_proc.StartInfo.FileName = Path.Combine(AppContext.BaseDirectory, "Media Player.exe");
                media_player_proc.Start();
                Directory.Delete(newFolder, true);
                Directory.Delete(tempOldFolder, true);
                Environment.Exit(0);
            }
            catch (HttpRequestException ex2)
            {
                mainWindow.ShowNetworkError();
                MessageBox.Show($"Failed to download update: {ex2.Message}\n{ex2.StackTrace}\n{ex2.InnerException}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Something went wrong while attempting to update to version: {GetLatestVersion()}\n{ex.Message}\n{ex.StackTrace}\n{ex.InnerException}");
                Debug.WriteLine($"{ex.Message}\n{ex.StackTrace}\n{ex.InnerException}");
                // deletes download file if something went wrong
                try
                {
                    if (File.Exists(fileToWrite)) File.Delete(fileToWrite);
                    if (Directory.Exists(newFolder)) Directory.Delete(newFolder, true);
                }
                catch (Exception ex2)
                {
                    MessageBox.Show($"Cannot delete update files: {ex2.Message}");
                }
            }
        }

        public static async Task<string> GetLatestVersion()
        {
            var response = await client.GetStringAsync(
                              "https://api.github.com/repos/vortex3225/media_player/releases/latest"
                          );
            var json = JsonNode.Parse(response);
            var tag = json?["tag_name"]?.ToString();
            return string.IsNullOrWhiteSpace(tag)
                    ? CURRENT_APP_VERSION
                    : tag.TrimStart('v');
        }

        public static string? GetCurrentVersion()
        {
            string? version = FileVersionInfo.GetVersionInfo(Path.Combine(AppContext.BaseDirectory, "Media Player.exe")).FileVersion;
            return version;
        }

        public static async Task<bool> IsCurrentLatest()
        {
            Version currentVer = Version.Parse(CURRENT_APP_VERSION);
            Version fetchedVer = Version.Parse(await GetLatestVersion());

            return currentVer.Equals(fetchedVer);
        }

        public static async Task CheckForUpdates()
        {
            try
            {
                if (string.IsNullOrEmpty(CURRENT_APP_VERSION))
                    throw new Exception("Could not fetch current application version!");

                bool is_latest = await IsCurrentLatest();
                if (is_latest)
                {
                    // run media player exe
                    Process m_proc = new Process();
                    m_proc.StartInfo.FileName = Path.Combine(AppContext.BaseDirectory, "Media Player.exe");
                    m_proc.Start();
                    Environment.Exit(0);
                }
                else DownloadUpdate();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Something went wrong while checking for updates: {ex.Message}\n{ex.StackTrace}");
            }
        }

    }
}
