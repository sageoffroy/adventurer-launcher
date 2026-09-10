using System.Windows;
using AdventurerLauncher.Services;

namespace AdventurerLauncher;

public partial class App : Application
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(10)
    };

    private async void Application_Startup(object sender, StartupEventArgs e)
    {
        var selfUpdater = new SelfUpdateService(HttpClient);
        if (await selfUpdater.TryStartUpdateAsync())
        {
            Shutdown();
            return;
        }

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }
}
