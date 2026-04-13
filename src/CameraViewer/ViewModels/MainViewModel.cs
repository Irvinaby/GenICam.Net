using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using CameraViewer.Demo;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenICam.Net.GenApi;
using GenICam.Net.GigEVision.Gvcp;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;

namespace CameraViewer.ViewModels;

/// <summary>
/// Root view-model. Owns the camera list, node tree, and stream sub-view-models.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly ILogger<MainViewModel> _logger;

    [ObservableProperty]
    private string _title = "CameraViewer";

    [ObservableProperty]
    private bool _isCameraConnected;

    [ObservableProperty]
    private string _connectionStatus = "Not connected";

    public CameraViewModel CameraVm { get; }
    public NodeTreeViewModel NodeTreeVm { get; }
    public StreamViewModel StreamVm { get; }

    private GvcpClient? _gvcpClient;
    private GigECameraInfo? _connectedCamera;
    private NodeMap? _nodeMap;

    public MainViewModel(Dispatcher dispatcher, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<MainViewModel>();
        CameraVm = new CameraViewModel(loggerFactory.CreateLogger<CameraViewModel>());
        NodeTreeVm = new NodeTreeViewModel();
        StreamVm = new StreamViewModel(dispatcher, loggerFactory.CreateLogger<StreamViewModel>());

        CameraVm.CameraConnectRequested += OnCameraConnectRequested;
        _logger.LogInformation("MainViewModel initialized");
    }

    private void OnCameraConnectRequested(object? sender, GigECameraInfo cam)
    {
        ConnectToCamera(cam);
    }

    private void ConnectToCamera(GigECameraInfo cam)
    {
        _logger.LogInformation("Connecting to camera at {IpAddress}", cam.IpAddress);
        ConnectionStatus = $"Connecting to {cam.IpAddress}…";
        IsCameraConnected = false;

        // Close any previous GVCP connection
        _gvcpClient?.Dispose();
        _gvcpClient = null;
        _connectedCamera = cam;

        // Establish GVCP connection to the camera
        var transport = new UdpTransportAdapter();
        _gvcpClient = new GvcpClient(transport, new IPEndPoint(cam.IpAddress, GvcpConstants.Port));
        _logger.LogDebug("GVCP client created for {IpAddress}:{Port}", cam.IpAddress, GvcpConstants.Port);

        // For demo purposes, load the synthetic node map.
        // In a real implementation, load from the camera's XML bootstrap register.
        _nodeMap = DemoNodeMapFactory.Create();
        NodeTreeVm.Load(_nodeMap);

        // Connect the node map to the GigE Vision port so commands and registers reach the camera
        var port = new GigEPort(_gvcpClient);
        _nodeMap.Connect(port);
        _logger.LogInformation("Node map connected to GigE port for {Model} ({IpAddress})", cam.ModelName, cam.IpAddress);

        Title = $"CameraViewer — {cam.ManufacturerName} {cam.ModelName} ({cam.IpAddress})";
        ConnectionStatus = $"Connected: {cam.ManufacturerName} {cam.ModelName}";
        IsCameraConnected = true;
    }

    [RelayCommand]
    private void LoadDemoCamera()
    {
        _logger.LogInformation("Loading demo camera (no hardware)");
        _nodeMap = DemoNodeMapFactory.Create();
        NodeTreeVm.Load(_nodeMap);
        Title = $"CameraViewer — Demo Camera (no hardware)";
        ConnectionStatus = "Connected: Demo Camera (no hardware)";
        IsCameraConnected = true;
    }

    [RelayCommand(CanExecute = nameof(IsCameraConnected))]
    private async Task StartAcquisitionAsync()
    {
        _logger.LogInformation("Starting acquisition");
        var localPort = StreamVm.StartStreaming();
        if (localPort == 0)
        {
            _logger.LogWarning("StartStreaming returned port 0; aborting acquisition start");
            return;
        }

        if (_gvcpClient is not null && _connectedCamera is not null)
        {
            try
            {
                // Determine which local IP reaches the camera
                var localIp = GetLocalIpForCamera(_connectedCamera.IpAddress);
                _logger.LogDebug("Local IP for camera: {LocalIp}", localIp);

                // Take exclusive control of the camera
                await _gvcpClient.WriteRegisterAsync(GvcpConstants.CcpRegister, 2);
                _logger.LogDebug("Exclusive control acquired (CCP register)");

                // Configure stream channel 0: destination address (host IP as big-endian uint32)
                var ipBytes = localIp.GetAddressBytes();
                var ipValue = BinaryPrimitives.ReadUInt32BigEndian(ipBytes);
                await _gvcpClient.WriteRegisterAsync(GvcpConstants.Scda0Register, ipValue);
                _logger.LogDebug("Stream destination configured: SCDA0={IpValue:X8}", ipValue);

                // Configure stream channel 0: host port in bits [31:16], enable in bit 0
                var scpValue = ((uint)localPort << 16) | 1u;
                await _gvcpClient.WriteRegisterAsync(GvcpConstants.Scp0Register, scpValue);
                _logger.LogDebug("Stream channel enabled: SCP0={ScpValue:X8} (port {Port})", scpValue, localPort);

                // Execute AcquisitionStart command to tell the camera to begin streaming
                var startCmd = _nodeMap?.GetNode("AcquisitionStart") as ICommand;
                if (startCmd is not null)
                {
                    startCmd.Execute();
                    _logger.LogInformation("AcquisitionStart command executed");
                }
                else
                {
                    _logger.LogWarning("AcquisitionStart node not found in node map");
                }

                ConnectionStatus = $"Streaming on port {localPort}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stream setup failed");
                ConnectionStatus = $"Stream setup failed: {ex.Message}";
            }
        }
        else
        {
            _logger.LogDebug("No GVCP client or camera; streaming locally only (demo mode)");
        }

        StartAcquisitionCommand.NotifyCanExecuteChanged();
        StopAcquisitionCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanStopAcquisition))]
    private async Task StopAcquisitionAsync()
    {
        _logger.LogInformation("Stopping acquisition");

        // Execute AcquisitionStop command to tell the camera to stop streaming
        var stopCmd = _nodeMap?.GetNode("AcquisitionStop") as ICommand;
        if (stopCmd is not null)
        {
            try
            {
                stopCmd.Execute();
                _logger.LogInformation("AcquisitionStop command executed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AcquisitionStop command failed");
            }
        }

        if (_gvcpClient is not null)
        {
            try
            {
                // Disable stream channel 0
                await _gvcpClient.WriteRegisterAsync(GvcpConstants.Scp0Register, 0);
                _logger.LogDebug("Stream channel disabled (SCP0 = 0)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to disable stream channel");
            }
        }

        StreamVm.StopStreaming();
        StartAcquisitionCommand.NotifyCanExecuteChanged();
        StopAcquisitionCommand.NotifyCanExecuteChanged();
    }

    private static IPAddress GetLocalIpForCamera(IPAddress cameraIp)
    {
        using var probe = new UdpClient();
        probe.Connect(cameraIp, GvcpConstants.Port);
        return ((IPEndPoint)probe.Client.LocalEndPoint!).Address;
    }

    private bool CanStopAcquisition() => IsCameraConnected && StreamVm.IsStreaming;

    partial void OnIsCameraConnectedChanged(bool value)
    {
        StartAcquisitionCommand.NotifyCanExecuteChanged();
        StopAcquisitionCommand.NotifyCanExecuteChanged();
    }
}
