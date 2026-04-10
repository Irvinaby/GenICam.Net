using CameraViewer.Demo;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenICam.Net.GenApi;
using GenICam.Net.GigEVision.Gvcp;
using System.Windows.Threading;

namespace CameraViewer.ViewModels;

/// <summary>
/// Root view-model. Owns the camera list, node tree, and stream sub-view-models.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "CameraViewer";

    [ObservableProperty]
    private bool _isCameraConnected;

    [ObservableProperty]
    private string _connectionStatus = "Not connected";

    public CameraViewModel CameraVm { get; }
    public NodeTreeViewModel NodeTreeVm { get; }
    public StreamViewModel StreamVm { get; }

    public MainViewModel(Dispatcher dispatcher)
    {
        CameraVm = new CameraViewModel();
        NodeTreeVm = new NodeTreeViewModel();
        StreamVm = new StreamViewModel(dispatcher);

        CameraVm.CameraConnectRequested += OnCameraConnectRequested;
    }

    private void OnCameraConnectRequested(object? sender, GigECameraInfo cam)
    {
        ConnectToCamera(cam);
    }

    private void ConnectToCamera(GigECameraInfo cam)
    {
        ConnectionStatus = $"Connecting to {cam.IpAddress}…";
        IsCameraConnected = false;

        // For demo purposes, load the synthetic node map.
        // In a real implementation, load from the camera's XML bootstrap register.
        var nodeMap = DemoNodeMapFactory.Create();
        NodeTreeVm.Load(nodeMap);

        Title = $"CameraViewer — {cam.ManufacturerName} {cam.ModelName} ({cam.IpAddress})";
        ConnectionStatus = $"Connected: {cam.ManufacturerName} {cam.ModelName}";
        IsCameraConnected = true;
    }

    [RelayCommand]
    private void LoadDemoCamera()
    {
        var nodeMap = DemoNodeMapFactory.Create();
        NodeTreeVm.Load(nodeMap);
        Title = $"CameraViewer — Demo Camera (no hardware)";
        ConnectionStatus = "Connected: Demo Camera (no hardware)";
        IsCameraConnected = true;
    }

    [RelayCommand(CanExecute = nameof(IsCameraConnected))]
    private void StartAcquisition()
    {
        StreamVm.StartStreaming();
        StartAcquisitionCommand.NotifyCanExecuteChanged();
        StopAcquisitionCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanStopAcquisition))]
    private void StopAcquisition()
    {
        StreamVm.StopStreaming();
        StartAcquisitionCommand.NotifyCanExecuteChanged();
        StopAcquisitionCommand.NotifyCanExecuteChanged();
    }

    private bool CanStopAcquisition() => IsCameraConnected && StreamVm.IsStreaming;

    partial void OnIsCameraConnectedChanged(bool value)
    {
        StartAcquisitionCommand.NotifyCanExecuteChanged();
        StopAcquisitionCommand.NotifyCanExecuteChanged();
    }
}
