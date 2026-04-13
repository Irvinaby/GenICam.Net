using System.Windows;
using System.Windows.Threading;
using CameraViewer.ViewModels;
using Microsoft.Extensions.Logging;

namespace CameraViewer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(LogLevel.Debug)
                .AddConsole();
        });

        var vm = new MainViewModel(Dispatcher.CurrentDispatcher, loggerFactory);
        var window = new Views.MainWindow { DataContext = vm };
        window.Show();
    }
}
