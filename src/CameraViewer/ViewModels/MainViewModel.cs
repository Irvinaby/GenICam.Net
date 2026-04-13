using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
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

    private GvcpClient? _gvcpClient;
    private GigECameraInfo? _connectedCamera;

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

        // Close any previous GVCP connection
        _gvcpClient?.Dispose();
        _gvcpClient = null;
        _connectedCamera = cam;

        // Establish GVCP connection to the camera
        var transport = new UdpTransportAdapter();
        _gvcpClient = new GvcpClient(transport, new IPEndPoint(cam.IpAddress, GvcpConstants.Port));

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
    private async Task StartAcquisitionAsync()
    {
        var localPort = StreamVm.StartStreaming();
        if (localPort == 0) return;

        if (_gvcpClient is not null && _connectedCamera is not null)
        {
            try
            {
                // Determine which local IP reaches the camera
                var localIp = GetLocalIpForCamera(_connectedCamera.IpAddress);

                // Take exclusive control of the camera
                await _gvcpClient.WriteRegisterAsync(GvcpConstants.CcpRegister, 2);

                // Configure stream channel 0: destination address (host IP as big-endian uint32)
                var ipBytes = localIp.GetAddressBytes();
                var ipValue = BinaryPrimitives.ReadUInt32BigEndian(ipBytes);
                await _gvcpClient.WriteRegisterAsync(GvcpConstants.Scda0Register, ipValue);

                // Configure stream channel 0: host port in bits [31:16], enable in bit 0
                var scpValue = ((uint)localPort << 16) | 1u;
                await _gvcpClient.WriteRegisterAsync(GvcpConstants.Scp0Register, scpValue);

                ConnectionStatus = $"Streaming on port {localPort}";
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"Stream setup failed: {ex.Message}";
            }
        }

        StartAcquisitionCommand.NotifyCanExecuteChanged();
        StopAcquisitionCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanStopAcquisition))]
    private async Task StopAcquisitionAsync()
    {
        if (_gvcpClient is not null)
        {
            try
            {
                // Disable stream channel 0
                await _gvcpClient.WriteRegisterAsync(GvcpConstants.Scp0Register, 0);
            }
            catch
            {
                // Best-effort cleanup
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
