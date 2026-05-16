using System.Windows.Threading;
using Autofac;
using CameraViewer.ViewModels;
using CameraViewer.Views;
using GenICam.Net.GigEVision.Gvcp;
using GenICam.Net.GigEVision.Gvsp;
using Microsoft.Extensions.Logging;

namespace CameraViewer.DependencyInjection;

public sealed class CameraViewerModule(Dispatcher dispatcher, ILoggerFactory loggerFactory) : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterInstance(dispatcher).AsSelf().SingleInstance().ExternallyOwned();
        builder.RegisterInstance(loggerFactory)
            .AsSelf()
            .As<ILoggerFactory>()
            .SingleInstance()
            .ExternallyOwned();
        builder.RegisterGeneric(typeof(Logger<>)).As(typeof(ILogger<>));

        builder.RegisterType<GigECameraDiscoveryService>()
            .As<IGigECameraDiscoveryService>()
            .InstancePerDependency();
        builder.RegisterType<GigECameraSessionFactory>()
            .As<IGigECameraSessionFactory>()
            .SingleInstance();
        builder.RegisterType<GvspStreamSession>()
            .As<IGvspStreamSession>()
            .InstancePerDependency();
        builder.RegisterType<GvspDisplayConverter>()
            .As<IGvspDisplayConverter>()
            .InstancePerDependency();

        builder.RegisterType<CameraViewModel>().SingleInstance();
        builder.RegisterType<NodeTreeViewModel>().SingleInstance();
        builder.RegisterType<StreamViewModel>().SingleInstance();
        builder.RegisterType<MainViewModel>().SingleInstance();

        builder.Register(context => new MainWindow
            {
                DataContext = context.Resolve<MainViewModel>(),
            })
            .AsSelf()
            .SingleInstance();
    }
}
