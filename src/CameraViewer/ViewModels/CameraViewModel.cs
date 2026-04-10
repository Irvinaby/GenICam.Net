using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenICam.Net.GigEVision.Gvcp;

namespace CameraViewer.ViewModels;

/// <summary>
/// Manages GigE Vision camera discovery and selection.
/// Exposes the discovered camera list and the currently selected camera.
/// </summary>
public sealed partial class CameraViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private GigECameraInfo? _selectedCamera;

    [ObservableProperty]
    private bool _isDiscovering;

    [ObservableProperty]
    private string _statusMessage = "Ready. Click Discover to find cameras.";

    public ObservableCollection<GigECameraInfo> Cameras { get; } = [];

    public event EventHandler<GigECameraInfo>? CameraConnectRequested;

    [RelayCommand]
    private async Task DiscoverAsync()
    {
        IsDiscovering = true;
        StatusMessage = "Discovering cameras…";
        Cameras.Clear();
        SelectedCamera = null;

        try
        {
            using var transport = new UdpTransportAdapter();
            using var discovery = new GigEDiscovery(transport);
            var cameras = await discovery.DiscoverAsync(timeoutMs: 2000);

            foreach (var cam in cameras)
                Cameras.Add(cam);

            StatusMessage = cameras.Count == 0
                ? "No cameras found. Check network and try again."
                : $"Found {cameras.Count} camera(s).";
        }
        catch (Exception ex)
        {
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
            CameraConnectRequested?.Invoke(this, SelectedCamera);
    }

    private bool CanConnect() => SelectedCamera is not null;
}
