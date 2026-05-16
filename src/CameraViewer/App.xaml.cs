using System.IO;
using System.Windows;
using System.Windows.Threading;
using Autofac;
using CameraViewer.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace CameraViewer;

public partial class App : Application
{
    private ILoggerFactory? _loggerFactory;
    private IContainer? _container;

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

        var builder = new ContainerBuilder();
        builder.RegisterModule(new CameraViewerModule(Dispatcher.CurrentDispatcher, _loggerFactory));
        _container = builder.Build();

        var window = _container.Resolve<Views.MainWindow>();
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _container?.Dispose();
        _loggerFactory?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
