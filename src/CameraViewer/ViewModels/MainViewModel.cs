using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenICam.Net.GigEVision.Gvcp;
using Microsoft.Extensions.Logging;

namespace CameraViewer.ViewModels;

/// <summary>
/// Root view-model. Coordinates camera list, node tree, and stream UI state.
/// Camera protocol/session behavior lives in GigEVision.Net.
/// </summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private const int DefaultStreamPort = 50000;

    private readonly ILogger<MainViewModel> _logger;
    private readonly IGigECameraSessionFactory _cameraSessionFactory;

    [ObservableProperty]
    private string _title = "CameraViewer";

    [ObservableProperty]
    private bool _isCameraConnected;

    [ObservableProperty]
    private string _connectionStatus = "Not connected";

    public CameraViewModel CameraVm { get; }
    public NodeTreeViewModel NodeTreeVm { get; }
    public StreamViewModel StreamVm { get; }

    private IGigECameraSession? _cameraSession;

    public MainViewModel(
        CameraViewModel cameraVm,
        NodeTreeViewModel nodeTreeVm,
        StreamViewModel streamVm,
        IGigECameraSessionFactory cameraSessionFactory,
        ILogger<MainViewModel> logger)
    {
        CameraVm = cameraVm;
        NodeTreeVm = nodeTreeVm;
        StreamVm = streamVm;
        _cameraSessionFactory = cameraSessionFactory;
        _logger = logger;

        StreamVm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(StreamViewModel.IsStreaming))
            {
                StartAcquisitionCommand.NotifyCanExecuteChanged();
                StopAcquisitionCommand.NotifyCanExecuteChanged();
            }
        };

        CameraVm.CameraConnectRequested += OnCameraConnectRequested;
        _logger.LogInformation("MainViewModel initialized");
    }

    private async void OnCameraConnectRequested(object? sender, GigECameraInfo cam)
    {
        try
        {
            await ConnectToCameraAsync(cam);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to camera at {IpAddress}", cam.IpAddress);
            ConnectionStatus = $"Connection failed: {ex.Message}";
            IsCameraConnected = false;
        }
    }

    private async Task ConnectToCameraAsync(GigECameraInfo cam)
    {
        _logger.LogInformation("Connecting to camera at {IpAddress}", cam.IpAddress);
        ConnectionStatus = $"Connecting to {cam.IpAddress}...";
        IsCameraConnected = false;

        if (_cameraSession is not null && StreamVm.IsStreaming)
        {
            try
            {
                await Task.Run(() => _cameraSession.StopAcquisitionAsync());
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not stop previous acquisition before reconnecting");
            }

            StreamVm.StopStreaming();
        }

        _cameraSession?.Dispose();
        _cameraSession = null;

        ConnectionStatus = $"Loading camera XML from {cam.IpAddress}...";
        _cameraSession = await Task.Run(() => _cameraSessionFactory.ConnectAsync(cam));

        NodeTreeVm.Load(_cameraSession.NodeMap);

        Title = $"CameraViewer - {cam.ManufacturerName} {cam.ModelName} ({cam.IpAddress})";
        ConnectionStatus = $"Connected: {cam.ManufacturerName} {cam.ModelName}";
        IsCameraConnected = true;
    }

    [RelayCommand(CanExecute = nameof(CanStartAcquisition))]
    private async Task StartAcquisitionAsync()
    {
        if (_cameraSession is null)
            return;

        _logger.LogInformation("Starting acquisition");
        var localPort = StreamVm.StartStreaming(DefaultStreamPort);
        if (localPort == 0)
        {
            _logger.LogWarning("StartStreaming returned port 0; aborting acquisition start");
            return;
        }

        try
        {
            await Task.Run(() => _cameraSession.StartAcquisitionAsync(localPort));
            ConnectionStatus = $"Streaming on port {localPort}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stream setup failed");
            ConnectionStatus = $"Stream setup failed: {ex.Message}";
            StreamVm.StopStreaming();
        }
        finally
        {
            StartAcquisitionCommand.NotifyCanExecuteChanged();
            StopAcquisitionCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanStopAcquisition))]
    private async Task StopAcquisitionAsync()
    {
        _logger.LogInformation("Stopping acquisition");

        if (_cameraSession is not null)
            await Task.Run(() => _cameraSession.StopAcquisitionAsync());

        StreamVm.StopStreaming();
        StartAcquisitionCommand.NotifyCanExecuteChanged();
        StopAcquisitionCommand.NotifyCanExecuteChanged();
    }

    private bool CanStopAcquisition() => IsCameraConnected && StreamVm.IsStreaming;

    private bool CanStartAcquisition() => IsCameraConnected && !StreamVm.IsStreaming;

    partial void OnIsCameraConnectedChanged(bool value)
    {
        StartAcquisitionCommand.NotifyCanExecuteChanged();
        StopAcquisitionCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        StreamVm.Dispose();
        _cameraSession?.Dispose();
    }
}
