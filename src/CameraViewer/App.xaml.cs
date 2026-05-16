using System.IO;
using System.Windows;
using System.Windows.Threading;
using CameraViewer.ViewModels;
using Microsoft.Extensions.Logging;
using Serilog;

namespace CameraViewer;

public partial class App : Application
{
    private ILoggerFactory? _loggerFactory;
    private MainViewModel? _mainViewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var logPath = Path.Combine(AppContext.BaseDirectory, "logs", "cameraviewer-.log");
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(logPath,
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(LogLevel.Debug)
                .AddSerilog(dispose: true);
        });

        _mainViewModel = new MainViewModel(Dispatcher.CurrentDispatcher, _loggerFactory);
        var window = new Views.MainWindow { DataContext = _mainViewModel };
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mainViewModel?.Dispose();
        _loggerFactory?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
