using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenICam.Net.GigEVision.Gvcp;
using GenICam.Net.GigEVision.Gvsp;

namespace CameraViewer.ViewModels;

/// <summary>
/// Manages GVSP image streaming from a GigE Vision camera.
/// Receives frames, renders them to a <see cref="WriteableBitmap"/>, and tracks FPS.
/// </summary>
public sealed partial class StreamViewModel : ObservableObject, IDisposable
{
    private GvspReceiver? _receiver;
    private CancellationTokenSource? _cts;
    private WriteableBitmap? _bitmap;
    private readonly Dispatcher _dispatcher;

    private int _frameCount;
    private DateTime _lastFpsTime = DateTime.UtcNow;

    [ObservableProperty]
    private bool _isStreaming;

    [ObservableProperty]
    private string _statusText = "Not streaming";

    [ObservableProperty]
    private double _fps;

    [ObservableProperty]
    private string _resolution = string.Empty;

    public WriteableBitmap? Bitmap
    {
        get => _bitmap;
        private set => SetProperty(ref _bitmap, value);
    }

    public StreamViewModel(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Starts the GVSP receive loop on the specified UDP port.
    /// </summary>
    /// <param name="streamPort">The UDP port on which the camera sends GVSP packets (typically 0 for auto-bind).</param>
    /// <returns>The local UDP port the receiver is bound to.</returns>
    public int StartStreaming(int streamPort = 0)
    {
        if (IsStreaming) return 0;

        _cts = new CancellationTokenSource();
        var udpClient = new System.Net.Sockets.UdpClient(streamPort);
        var localPort = ((System.Net.IPEndPoint)udpClient.Client.LocalEndPoint!).Port;
        var transport = new UdpTransportAdapter(udpClient);

        _receiver = new GvspReceiver(transport);
        _receiver.FrameReceived += OnFrameReceived;

        IsStreaming = true;
        StatusText = "Streaming…";

        _ = _receiver.StartAsync(_cts.Token);

        return localPort;
    }

    [RelayCommand(CanExecute = nameof(IsNotStreaming))]
    private void Start() => StartStreaming();

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop() => StopStreaming();

    private bool IsNotStreaming() => !IsStreaming;
    private bool CanStop() => IsStreaming;

    private void OnFrameReceived(object? sender, GvspFrame frame)
    {
        _dispatcher.Invoke(() => RenderFrame(frame));
    }

    private void RenderFrame(GvspFrame frame)
    {
        var width = (int)frame.SizeX;
        var height = (int)frame.SizeY;
        if (width <= 0 || height <= 0) return;

        // Determine pixel format for WriteableBitmap
        // We support Mono8 (0x01080001) and Mono16 (0x01100007); treat others as Mono8 best-effort.
        var isMono16 = frame.PixelFormat == 0x01100007;
        var expectedBytes = isMono16 ? width * height * 2 : width * height;
        if (frame.Data.Length < expectedBytes) return;

        if (_bitmap is null || _bitmap.PixelWidth != width || _bitmap.PixelHeight != height)
        {
            _bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Gray8, null);
            Bitmap = _bitmap;
        }

        _bitmap.Lock();
        try
        {
            if (isMono16)
            {
                // Scale 16-bit → 8-bit for display
                unsafe
                {
                    var dst = (byte*)_bitmap.BackBuffer;
                    var src = frame.Data;
                    for (int i = 0; i < width * height; i++)
                    {
                        ushort raw = (ushort)(src[i * 2] | (src[i * 2 + 1] << 8));
                        dst[i] = (byte)(raw >> 8);
                    }
                }
            }
            else
            {
                System.Runtime.InteropServices.Marshal.Copy(frame.Data, 0, _bitmap.BackBuffer, width * height);
            }
            _bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
        }
        finally
        {
            _bitmap.Unlock();
        }

        // FPS calculation
        _frameCount++;
        var now = DateTime.UtcNow;
        var elapsed = (now - _lastFpsTime).TotalSeconds;
        if (elapsed >= 1.0)
        {
            Fps = Math.Round(_frameCount / elapsed, 1);
            _frameCount = 0;
            _lastFpsTime = now;
        }

        Resolution = $"{width} × {height}";
        StatusText = $"{Fps:F1} fps  |  {Resolution}";
    }

    public void StopStreaming()
    {
        if (!IsStreaming) return;
        _cts?.Cancel();
        if (_receiver is not null)
        {
            _receiver.FrameReceived -= OnFrameReceived;
            _receiver.Dispose();
            _receiver = null;
        }
        _cts?.Dispose();
        _cts = null;
        IsStreaming = false;
        StatusText = "Stopped";
        StopCommand.NotifyCanExecuteChanged();
        StartCommand.NotifyCanExecuteChanged();
    }

    public void Dispose() => StopStreaming();
}
