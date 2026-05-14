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
    private GvspFrame? _pendingRenderFrame;
    private int _renderQueued;

    private int _frameCount;
    private int _receivedFrameCount;
    private int _completedFrameCount;
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

    [ObservableProperty]
    private string _imageStats = string.Empty;

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
            ImageStats = string.Empty;
            _frameCount = 0;
            _receivedFrameCount = 0;
            _completedFrameCount = 0;
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
        Interlocked.Increment(ref _completedFrameCount);
        Interlocked.Exchange(ref _pendingRenderFrame, frame);
        QueueRender();
    }

    private void QueueRender()
    {
        if (Interlocked.Exchange(ref _renderQueued, 1) != 0)
            return;

        _dispatcher.BeginInvoke(RenderPendingFrame);
    }

    private void RenderPendingFrame()
    {
        var frame = Interlocked.Exchange(ref _pendingRenderFrame, null);
        if (frame is not null)
            RenderFrame(frame);

        Interlocked.Exchange(ref _renderQueued, 0);
        if (Volatile.Read(ref _pendingRenderFrame) is not null)
            QueueRender();
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
                StatusText = $"Receiving UDP ({UdpPacketCount} packets; {PacketSummary}), completed {Volatile.Read(ref _completedFrameCount)} frame(s), waiting for render...";
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

        // Convert the camera payload into a WPF-friendly display buffer.
        if (!TryConvertForDisplay(frame, width, height, out var displayData, out var displayFormat, out var stride, out var formatName, out var imageStats))
            return;

        if (_bitmap is null ||
            _bitmap.PixelWidth != width ||
            _bitmap.PixelHeight != height ||
            _bitmap.Format != displayFormat)
        {
            _logger.LogInformation("Creating bitmap {Width}x{Height} ({FormatName}, PixelFormat=0x{PixelFormat:X8})",
                width, height, formatName, frame.PixelFormat);
            _bitmap = new WriteableBitmap(width, height, 96, 96, displayFormat, null);
            Bitmap = _bitmap;
        }

        _bitmap.WritePixels(new Int32Rect(0, 0, width, height), displayData, stride, 0);

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

        Resolution = $"{width} x {height} {formatName}";
        ImageStats = imageStats;
        UdpPacketCount = _streamTransport?.ReceivedPacketCount ?? UdpPacketCount;
        PacketSummary = _streamTransport?.PacketSummary ?? PacketSummary;
        StatusText = $"{Fps:F1} fps  |  {Resolution}  |  rendered {ReceivedFrames}, completed {Volatile.Read(ref _completedFrameCount)}  |  {imageStats}  |  {UdpPacketCount} packets  |  {PacketSummary}";
    }

    private bool TryConvertForDisplay(
        GvspFrame frame,
        int width,
        int height,
        out byte[] displayData,
        out PixelFormat displayFormat,
        out int stride,
        out string formatName,
        out string imageStats)
    {
        displayFormat = PixelFormats.Gray8;
        stride = width;
        formatName = GetPixelFormatName(frame.PixelFormat);
        imageStats = GetByteStats(frame.Data);
        displayData = [];

        var pixelCount = checked(width * height);
        switch (frame.PixelFormat)
        {
            case 0x01080001:
            case 0x01080008:
            case 0x01080009:
            case 0x0108000A:
            case 0x0108000B:
                if (!HasEnoughData(frame, pixelCount, formatName))
                    return false;
                displayData = new byte[pixelCount];
                Buffer.BlockCopy(frame.Data, 0, displayData, 0, pixelCount);
                return true;
            case 0x01100003:
                return ConvertUnpackedMono(frame, pixelCount, 10, formatName, out displayData);
            case 0x01100005:
                return ConvertUnpackedMono(frame, pixelCount, 12, formatName, out displayData);
            case 0x01100007:
                return ConvertUnpackedMono(frame, pixelCount, 16, formatName, out displayData);
            case 0x010C0004:
                return ConvertMono10Packed(frame, pixelCount, formatName, out displayData);
            case 0x010C0006:
                return ConvertMono12Packed(frame, pixelCount, formatName, out displayData);
            case 0x02180014:
                displayFormat = PixelFormats.Bgr24;
                stride = width * 3;
                return ConvertRgb(frame, pixelCount, formatName, swapRedBlue: true, out displayData);
            case 0x02180015:
                displayFormat = PixelFormats.Bgr24;
                stride = width * 3;
                return ConvertRgb(frame, pixelCount, formatName, swapRedBlue: false, out displayData);
            case 0x02200016:
                displayFormat = PixelFormats.Bgr32;
                stride = width * 4;
                return ConvertRgba(frame, pixelCount, formatName, swapRedBlue: true, out displayData);
            case 0x02200017:
                displayFormat = PixelFormats.Bgr32;
                stride = width * 4;
                return ConvertRgba(frame, pixelCount, formatName, swapRedBlue: false, out displayData);
            default:
                if (!HasEnoughData(frame, pixelCount, formatName))
                    return false;
                _logger.LogWarning("Unsupported pixel format 0x{PixelFormat:X8}; displaying first byte per pixel as grayscale", frame.PixelFormat);
                displayData = new byte[pixelCount];
                Buffer.BlockCopy(frame.Data, 0, displayData, 0, pixelCount);
                return true;
        }
    }

    private bool ConvertUnpackedMono(GvspFrame frame, int pixelCount, int significantBits, string formatName, out byte[] displayData)
    {
        displayData = [];
        if (!HasEnoughData(frame, pixelCount * 2, formatName))
            return false;

        var values = new ushort[pixelCount];
        ushort min = ushort.MaxValue;
        ushort max = ushort.MinValue;
        for (var i = 0; i < pixelCount; i++)
        {
            var value = (ushort)(frame.Data[i * 2] | (frame.Data[i * 2 + 1] << 8));
            if (significantBits < 16)
                value = (ushort)Math.Min(value, (1 << significantBits) - 1);
            values[i] = value;
            min = Math.Min(min, value);
            max = Math.Max(max, value);
        }

        displayData = ScaleMono(values, min, max);
        return true;
    }

    private bool ConvertMono10Packed(GvspFrame frame, int pixelCount, string formatName, out byte[] displayData)
    {
        displayData = [];
        var expectedBytes = (pixelCount * 10 + 7) / 8;
        if (!HasEnoughData(frame, expectedBytes, formatName))
            return false;

        var values = new ushort[pixelCount];
        ushort min = ushort.MaxValue;
        ushort max = ushort.MinValue;
        var source = frame.Data;
        var src = 0;
        var dst = 0;
        while (dst < pixelCount)
        {
            var b0 = source[src++];
            var b1 = src < source.Length ? source[src++] : 0;
            var b2 = src < source.Length ? source[src++] : 0;
            var b3 = src < source.Length ? source[src++] : 0;
            var b4 = src < source.Length ? source[src++] : 0;
            AddPackedValue((ushort)(b0 | ((b1 & 0x03) << 8)));
            AddPackedValue((ushort)((b1 >> 2) | ((b2 & 0x0F) << 6)));
            AddPackedValue((ushort)((b2 >> 4) | ((b3 & 0x3F) << 4)));
            AddPackedValue((ushort)((b3 >> 6) | (b4 << 2)));
        }

        displayData = ScaleMono(values, min, max);
        return true;

        void AddPackedValue(ushort value)
        {
            if (dst >= pixelCount)
                return;
            values[dst++] = value;
            min = Math.Min(min, value);
            max = Math.Max(max, value);
        }
    }

    private bool ConvertMono12Packed(GvspFrame frame, int pixelCount, string formatName, out byte[] displayData)
    {
        displayData = [];
        var expectedBytes = (pixelCount * 12 + 7) / 8;
        if (!HasEnoughData(frame, expectedBytes, formatName))
            return false;

        var values = new ushort[pixelCount];
        ushort min = ushort.MaxValue;
        ushort max = ushort.MinValue;
        var source = frame.Data;
        var src = 0;
        var dst = 0;
        while (dst < pixelCount)
        {
            var b0 = source[src++];
            var b1 = src < source.Length ? source[src++] : 0;
            var b2 = src < source.Length ? source[src++] : 0;
            AddPackedValue((ushort)(b0 | ((b1 & 0x0F) << 8)));
            AddPackedValue((ushort)((b1 >> 4) | (b2 << 4)));
        }

        displayData = ScaleMono(values, min, max);
        return true;

        void AddPackedValue(ushort value)
        {
            if (dst >= pixelCount)
                return;
            values[dst++] = value;
            min = Math.Min(min, value);
            max = Math.Max(max, value);
        }
    }

    private bool ConvertRgb(GvspFrame frame, int pixelCount, string formatName, bool swapRedBlue, out byte[] displayData)
    {
        displayData = [];
        var expectedBytes = pixelCount * 3;
        if (!HasEnoughData(frame, expectedBytes, formatName))
            return false;

        displayData = new byte[expectedBytes];
        for (var src = 0; src < expectedBytes; src += 3)
        {
            displayData[src] = swapRedBlue ? frame.Data[src + 2] : frame.Data[src];
            displayData[src + 1] = frame.Data[src + 1];
            displayData[src + 2] = swapRedBlue ? frame.Data[src] : frame.Data[src + 2];
        }
        return true;
    }

    private bool ConvertRgba(GvspFrame frame, int pixelCount, string formatName, bool swapRedBlue, out byte[] displayData)
    {
        displayData = [];
        var expectedBytes = pixelCount * 4;
        if (!HasEnoughData(frame, expectedBytes, formatName))
            return false;

        displayData = new byte[expectedBytes];
        for (var src = 0; src < expectedBytes; src += 4)
        {
            displayData[src] = swapRedBlue ? frame.Data[src + 2] : frame.Data[src];
            displayData[src + 1] = frame.Data[src + 1];
            displayData[src + 2] = swapRedBlue ? frame.Data[src] : frame.Data[src + 2];
            displayData[src + 3] = 255;
        }
        return true;
    }

    private static byte[] ScaleMono(ushort[] values, ushort min, ushort max)
    {
        var displayData = new byte[values.Length];
        if (max <= min)
        {
            if (max > 0)
                Array.Fill(displayData, (byte)255);
            return displayData;
        }

        var scale = 255.0 / (max - min);
        for (var i = 0; i < values.Length; i++)
            displayData[i] = (byte)Math.Clamp((values[i] - min) * scale, 0, 255);

        return displayData;
    }

    private bool HasEnoughData(GvspFrame frame, int expectedBytes, string formatName)
    {
        if (frame.Data.Length >= expectedBytes)
            return true;

        _logger.LogWarning("Frame {FrameId} {FormatName} data too short: {Actual} < {Expected} bytes",
            frame.FrameId, formatName, frame.Data.Length, expectedBytes);
        return false;
    }

    private static string GetPixelFormatName(uint pixelFormat) => pixelFormat switch
    {
        0x01080001 => "Mono8",
        0x01100003 => "Mono10",
        0x010C0004 => "Mono10Packed",
        0x01100005 => "Mono12",
        0x010C0006 => "Mono12Packed",
        0x01100007 => "Mono16",
        0x01080008 => "BayerGR8",
        0x01080009 => "BayerRG8",
        0x0108000A => "BayerGB8",
        0x0108000B => "BayerBG8",
        0x02180014 => "RGB8",
        0x02180015 => "BGR8",
        0x02200016 => "RGBA8",
        0x02200017 => "BGRA8",
        _ => $"0x{pixelFormat:X8}",
    };

    private static string GetByteStats(byte[] data)
    {
        if (data.Length == 0)
            return "empty payload";

        byte min = byte.MaxValue;
        byte max = byte.MinValue;
        long sum = 0;
        foreach (var value in data)
        {
            min = Math.Min(min, value);
            max = Math.Max(max, value);
            sum += value;
        }

        return $"raw min:{min} max:{max} avg:{sum / (double)data.Length:F1}";
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
