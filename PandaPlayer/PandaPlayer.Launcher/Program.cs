using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace PandaPlayer.Launcher
{
    /// <summary>
    /// Entry point for Panda Player application.
    /// Handles command-line arguments for file/folder launching.
    /// </summary>
    public class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                var app = new App();
                
                if (args.Length > 0)
                {
                    // Handle command-line launch from Explorer
                    string path = args[0];
                    if (File.Exists(path) || Directory.Exists(path))
                    {
                        // Pass path to main window
                        app.Resources["LaunchPath"] = path;
                    }
                }

                app.Run();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fatal error: {ex.Message}", "Panda Player Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// Application class for launcher.
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
        }
    }
}
