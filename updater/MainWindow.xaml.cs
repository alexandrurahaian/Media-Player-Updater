using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using updater.Scripts;

namespace updater
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static bool GetAutoUpdateStatus()
        {
            string auto_update_file = System.IO.Path.Combine(AppContext.BaseDirectory, "auto_update.cfg");
            if (System.IO.File.Exists(auto_update_file))
            {
                string contents = System.IO.File.ReadAllText(auto_update_file);
                return contents.Contains("true");
            }
            return false;
        }

        private static void ClearOldBackups()
        {
            const int MIN_AGE = 4; // backups older than MIN_AGE days will be deleted.
            string backupDir = System.IO.Path.Combine(AppContext.BaseDirectory, "Backups");
            if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);

            foreach (string file in System.IO.Directory.GetFiles(System.IO.Path.Combine(AppContext.BaseDirectory, "Backups")))
            {
                try
                {
                    DateTime creationDateUtc = System.IO.File.GetLastWriteTimeUtc(file);
                    TimeSpan diff = DateTime.UtcNow - creationDateUtc;
                    if (diff.TotalDays >= MIN_AGE)
                        System.IO.File.Delete(file);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not clear old backup: {file}\n{ex.Message}\n{ex.StackTrace}\n{ex.InnerException}");
                }
            }
        }

        public MainWindow()
        {
            ClearOldBackups();
            VersionHandler.InitClient(this);
            InitializeComponent();
            this.WindowState = WindowState.Minimized;
            if (GetAutoUpdateStatus() == true)
                this.WindowState = WindowState.Minimized;
            SetVersions();
            CheckUpdates();
        }

        public void ShowNetworkError()
        {
            error_box.Visibility = Visibility.Visible;
        }

        private async void SetVersions()
        {
            string current_ver = VersionHandler.GetCurrentVersion() ?? "?.?.?";
            string latest_ver = await VersionHandler.GetLatestVersion();

            current_version_box.Text = $"Current version: {current_ver}";
            latest_version_box.Text = $"Latest version: {latest_ver}";
        }

        private async void CheckUpdates()
        {
            bool is_already_latest = await VersionHandler.IsCurrentLatest();
            if (is_already_latest)
            {
                Process media_proc = new Process();
                media_proc.StartInfo.FileName = System.IO.Path.Combine(AppContext.BaseDirectory, "Media Player.exe");
                media_proc.Start();
                Environment.Exit(0);
            }
            else if (GetAutoUpdateStatus() == true)
                await VersionHandler.CheckForUpdates();
            else
            {
                this.WindowState = WindowState.Normal;
                this.Activate();
                update_btn.IsEnabled = true;
            }
        }

        private async void update_btn_Click(object sender, RoutedEventArgs e)
        {
           await VersionHandler.CheckForUpdates();
        }

        private void skip_btn_Click(object sender, RoutedEventArgs e)
        {
            Process media_proc = new Process();
            media_proc.StartInfo.FileName = System.IO.Path.Combine(AppContext.BaseDirectory, "Media Player.exe");
            media_proc.Start();
            Environment.Exit(0);
        }

        private void open_changelog_btn_Click(object sender, RoutedEventArgs e)
        {
           const string url = "https://github.com/alexandrurahaian/media_player/releases/latest";
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{ex.Message}\n{ex.StackTrace}\n{ex.InnerException}");
            }
        }
    }
}