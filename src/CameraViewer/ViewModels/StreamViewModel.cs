using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenICam.Net.GigEVision.Gvcp;
using GenICam.Net.GigEVision.Gvsp;
using Microsoft.Extensions.Logging;
using System.Net;

namespace CameraViewer.ViewModels;

/// <summary>
/// Manages GVSP image streaming from a GigE Vision camera.
/// Receives frames, renders them to a <see cref="WriteableBitmap"/>, and tracks FPS.
/// </summary>
public sealed partial class StreamViewModel : ObservableObject, IDisposable
{
    private readonly ILogger<StreamViewModel> _logger;
    private GvspReceiver? _receiver;
    private CancellationTokenSource? _cts;
    private WriteableBitmap? _bitmap;
    private readonly Dispatcher _dispatcher;
    private CountingUdpTransport? _streamTransport;

    private int _frameCount;
    private int _receivedFrameCount;
    private DateTime _lastFpsTime = DateTime.UtcNow;

    [ObservableProperty]
    private bool _isStreaming;

    [ObservableProperty]
    private string _statusText = "Not streaming";

    [ObservableProperty]
    private double _fps;

    [ObservableProperty]
    private string _resolution = string.Empty;

    [ObservableProperty]
    private int _udpPacketCount;

    [ObservableProperty]
    private int _receivedFrames;

    [ObservableProperty]
    private string _packetSummary = string.Empty;

    public WriteableBitmap? Bitmap
    {
        get => _bitmap;
        private set => SetProperty(ref _bitmap, value);
    }

    public StreamViewModel(Dispatcher dispatcher, ILogger<StreamViewModel> logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
    }

    /// <summary>
    /// Starts the GVSP receive loop on the specified UDP port.
    /// </summary>
    /// <param name="streamPort">The UDP port on which the camera sends GVSP packets (typically 0 for auto-bind).</param>
    /// <returns>The local UDP port the receiver is bound to.</returns>
    public int StartStreaming(int streamPort = 0)
    {
        if (IsStreaming)
        {
            _logger.LogWarning("StartStreaming called while already streaming");
            return 0;
        }

        try
        {
            _cts = new CancellationTokenSource();
            var udpClient = new System.Net.Sockets.UdpClient(streamPort);
            udpClient.Client.ReceiveBufferSize = 64 * 1024 * 1024;
            var localPort = ((System.Net.IPEndPoint)udpClient.Client.LocalEndPoint!).Port;
            _streamTransport = new CountingUdpTransport(new UdpTransportAdapter(udpClient), OnUdpPacketReceived);

            _receiver = new GvspReceiver(_streamTransport);
            _receiver.FrameReceived += OnFrameReceived;

            IsStreaming = true;
            Fps = 0;
            UdpPacketCount = 0;
            ReceivedFrames = 0;
            PacketSummary = string.Empty;
            _frameCount = 0;
            _receivedFrameCount = 0;
            _lastFpsTime = DateTime.UtcNow;
            StatusText = "Streaming…";

            _logger.LogInformation("GVSP receiver started on local port {Port}", localPort);
            _ = RunReceiveLoopAsync();

            return localPort;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start streaming");
            return 0;
        }
    }

    [RelayCommand(CanExecute = nameof(IsNotStreaming))]
    private void Start() => StartStreaming();

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop() => StopStreaming();

    private bool IsNotStreaming() => !IsStreaming;
    private bool CanStop() => IsStreaming;

    partial void OnIsStreamingChanged(bool value)
    {
        StartCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
    }

    private async Task RunReceiveLoopAsync()
    {
        try
        {
            await _receiver!.StartAsync(_cts!.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected on stop
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GVSP receive loop terminated with error");
        }
    }

    private void OnFrameReceived(object? sender, GvspFrame frame)
    {
        _dispatcher.Invoke(() => RenderFrame(frame));
    }

    private void OnUdpPacketReceived(int packetCount, int packetLength)
    {
        if (packetCount <= 5)
            _logger.LogDebug("GVSP UDP packet {PacketCount}: {Length} bytes", packetCount, packetLength);

        if (packetCount % 500 != 0)
            return;

        _dispatcher.BeginInvoke(() =>
        {
            UdpPacketCount = packetCount;
            PacketSummary = _streamTransport?.PacketSummary ?? PacketSummary;
            if (_receivedFrameCount == 0)
                StatusText = $"Receiving UDP ({UdpPacketCount} packets; {PacketSummary}), waiting for complete frame...";
        });
    }

    private void RenderFrame(GvspFrame frame)
    {
        var width = (int)frame.SizeX;
        var height = (int)frame.SizeY;
        if (width <= 0 || height <= 0)
        {
            _logger.LogWarning("Frame {FrameId} has invalid dimensions: {Width}x{Height}", frame.FrameId, width, height);
            return;
        }

        // Determine pixel format for WriteableBitmap
        // We support Mono8 (0x01080001) and Mono16 (0x01100007); treat others as Mono8 best-effort.
        var isMono16 = frame.PixelFormat == 0x01100007;
        var expectedBytes = isMono16 ? width * height * 2 : width * height;
        if (frame.Data.Length < expectedBytes)
        {
            _logger.LogWarning("Frame {FrameId} data too short: {Actual} < {Expected} bytes", frame.FrameId, frame.Data.Length, expectedBytes);
            return;
        }

        if (_bitmap is null || _bitmap.PixelWidth != width || _bitmap.PixelHeight != height)
        {
            _logger.LogInformation("Creating bitmap {Width}x{Height} (PixelFormat=0x{PixelFormat:X8})", width, height, frame.PixelFormat);
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
        _receivedFrameCount++;
        ReceivedFrames = _receivedFrameCount;
        _frameCount++;
        var now = DateTime.UtcNow;
        var elapsed = (now - _lastFpsTime).TotalSeconds;
        if (elapsed >= 1.0)
        {
            Fps = Math.Round(_frameCount / elapsed, 1);
            _logger.LogDebug("FPS: {Fps}, frames in window: {FrameCount}", Fps, _frameCount);
            _frameCount = 0;
            _lastFpsTime = now;
        }

        Resolution = $"{width} × {height}";
        UdpPacketCount = _streamTransport?.ReceivedPacketCount ?? UdpPacketCount;
        PacketSummary = _streamTransport?.PacketSummary ?? PacketSummary;
        StatusText = $"{Fps:F1} fps  |  {Resolution}  |  {ReceivedFrames} frames  |  {UdpPacketCount} packets  |  {PacketSummary}";
    }

    public void StopStreaming()
    {
        if (!IsStreaming) return;
        _logger.LogInformation("Stopping GVSP streaming");
        _cts?.Cancel();
        if (_receiver is not null)
        {
            _receiver.FrameReceived -= OnFrameReceived;
            _receiver.Dispose();
            _receiver = null;
        }
        _streamTransport = null;
        _cts?.Dispose();
        _cts = null;
        IsStreaming = false;
        StatusText = "Stopped";
        StopCommand.NotifyCanExecuteChanged();
        StartCommand.NotifyCanExecuteChanged();
    }

    public void Dispose() => StopStreaming();

    private sealed class CountingUdpTransport(IUdpTransport inner, Action<int, int> packetReceived) : IUdpTransport
    {
        public int ReceivedPacketCount { get; private set; }
        public int LeaderCount { get; private set; }
        public int TrailerCount { get; private set; }
        public int PayloadCount { get; private set; }
        public int OtherCount { get; private set; }
        public string PacketSummary => $"L:{LeaderCount} P:{PayloadCount} T:{TrailerCount} O:{OtherCount}";

        public Task SendAsync(byte[] data, IPEndPoint endPoint, CancellationToken cancellationToken = default)
            => inner.SendAsync(data, endPoint, cancellationToken);

        public async Task<GenICam.Net.GigEVision.Gvcp.UdpReceiveResult> ReceiveAsync(CancellationToken cancellationToken = default)
        {
            var result = await inner.ReceiveAsync(cancellationToken);
            ReceivedPacketCount++;
            CountPacketType(result.Buffer);
            packetReceived(ReceivedPacketCount, result.Buffer.Length);
            return result;
        }

        public void EnableBroadcast() => inner.EnableBroadcast();

        public void Dispose() => inner.Dispose();

        private void CountPacketType(byte[] packet)
        {
            if (packet.Length < GvspConstants.GenericHeaderSize)
            {
                OtherCount++;
                return;
            }

            switch ((GvspPacketType)packet[4])
            {
                case GvspPacketType.Leader:
                    LeaderCount++;
                    break;
                case GvspPacketType.Payload:
                    PayloadCount++;
                    break;
                case GvspPacketType.Trailer:
                    TrailerCount++;
                    break;
                default:
                    OtherCount++;
                    break;
            }
        }
    }
}
