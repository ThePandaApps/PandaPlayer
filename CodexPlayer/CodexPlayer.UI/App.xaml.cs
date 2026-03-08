using System;
using System.IO;
using System.Windows;
using System.Diagnostics;

namespace CodexPlayer.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static string _logPath;

        public App()
        {
            try
            {
                // Set up logging
                _logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CodexPlayer", "logs", $"app_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");
                
                EnsureLogDirectoryExists();
                LogMessage("=== Application Starting ===");
                LogMessage($"Base Directory: {AppDomain.CurrentDomain.BaseDirectory}");
                
                // Hook unhandled exceptions
                this.DispatcherUnhandledException += App_DispatcherUnhandledException;
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                
                // Manually load the main window instead of using StartupUri
                LogMessage("Creating MainWindow");
                MainWindow = new MainWindow();
                LogMessage("MainWindow created successfully");
                
                // Show the window
                MainWindow.Show();
                LogMessage("MainWindow shown");
            }
            catch (Exception ex)
            {
                LogMessage($"CRITICAL ERROR in App constructor: {ex.GetType().Name}: {ex.Message}");
                LogMessage($"STACK TRACE: {ex.StackTrace}");
                
                MessageBox.Show(
                    $"Failed to initialize application:\n\n{ex.Message}\n\nStack: {ex.StackTrace}",
                    "Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                
                Shutdown(1);
            }
        }

        private static void EnsureLogDirectoryExists()
        {
            var dir = Path.GetDirectoryName(_logPath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        public static void LogMessage(string message)
        {
            try
            {
                if (_logPath == null)
                    return;

                EnsureLogDirectoryExists();
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                var logEntry = $"[{timestamp}] {message}";
                File.AppendAllText(_logPath, logEntry + Environment.NewLine);
            }
            catch { }
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            LogMessage($"DISPATCHER EXCEPTION: {e.Exception.GetType().Name}: {e.Exception.Message}");
            LogMessage($"STACK TRACE: {e.Exception.StackTrace}");
            
            MessageBox.Show(
                $"Application Error:\n\n{e.Exception.Message}\n\nLog: {_logPath}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            LogMessage($"UNHANDLED EXCEPTION: {ex?.GetType().Name}: {ex?.Message}");
            LogMessage($"STACK TRACE: {ex?.StackTrace}");
            
            MessageBox.Show(
                $"Critical Error:\n\n{ex?.Message}\n\nLog: {_logPath}",
                "Critical Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
