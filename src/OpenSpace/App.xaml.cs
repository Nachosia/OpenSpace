using System.IO;
using System.Windows;

namespace OpenSpace;

public partial class App : Application
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OpenSpace",
        "error.log");

    public App()
    {
        LogException(new Exception($"[App] Starting OpenSpace {Core.VersionInfo.CurrentVersion}"));
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private static void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        LogException(e.Exception);
        MessageBox.Show($"Произошла ошибка:\n{e.Exception.Message}\n\nПодробности записаны в:\n{LogPath}",
            "OpenSpace Error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogException(ex);
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogException(e.Exception);
        e.SetObserved();
    }

    public static void LogException(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            var message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n\n";
            File.AppendAllText(LogPath, message);
        }
        catch
        {
            // If logging fails, there's not much we can do.
        }
    }
}
