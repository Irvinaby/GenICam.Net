using System.Windows;
using System.Windows.Threading;
using CameraViewer.ViewModels;

namespace CameraViewer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var vm = new MainViewModel(Dispatcher.CurrentDispatcher);
        var window = new Views.MainWindow { DataContext = vm };
        window.Show();
    }
}
