using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenICam.Net.GigEVision.Gvcp;
using Microsoft.Extensions.Logging;

namespace CameraViewer.ViewModels;

/// <summary>
/// Manages camera discovery and selection UI state.
/// </summary>
public sealed partial class CameraViewModel : ObservableObject
{
    private readonly IGigECameraDiscoveryService _discoveryService;
    private readonly ILogger<CameraViewModel> _logger;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private GigECameraInfo? _selectedCamera;

    [ObservableProperty]
    private bool _isDiscovering;

    [ObservableProperty]
    private string _statusMessage = "Ready. Click Discover to find cameras.";

    public ObservableCollection<GigECameraInfo> Cameras { get; } = [];

    public event EventHandler<GigECameraInfo>? CameraConnectRequested;

    public CameraViewModel(IGigECameraDiscoveryService discoveryService, ILogger<CameraViewModel> logger)
    {
        _discoveryService = discoveryService;
        _logger = logger;
    }

    [RelayCommand]
    private async Task DiscoverAsync()
    {
        IsDiscovering = true;
        StatusMessage = "Discovering cameras...";
        Cameras.Clear();
        SelectedCamera = null;
        _logger.LogInformation("Starting camera discovery");

        try
        {
            var cameras = await _discoveryService.DiscoverAsync(timeoutMs: 3000);

            foreach (var cam in cameras)
                Cameras.Add(cam);

            StatusMessage = cameras.Count == 0
                ? "No cameras found. Check network and try again."
                : $"Found {cameras.Count} camera(s).";

            _logger.LogInformation("Discovery complete: {Count} camera(s) found", cameras.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Camera discovery failed");
            StatusMessage = $"Discovery failed: {ex.Message}";
        }
        finally
        {
            IsDiscovering = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private void Connect()
    {
        if (SelectedCamera is not null)
        {
            _logger.LogInformation("Connect requested for camera at {IpAddress}", SelectedCamera.IpAddress);
            CameraConnectRequested?.Invoke(this, SelectedCamera);
        }
    }

    private bool CanConnect() => SelectedCamera is not null;
}
