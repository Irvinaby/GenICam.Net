using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GenICam.Net.GigEVision.Gvsp;
using Microsoft.Extensions.Logging;

namespace CameraViewer.ViewModels;

/// <summary>
/// Renders GVSP frames into WPF state. Receiving, packet stats, and pixel conversion live in GigEVision.Net.
/// </summary>
public sealed partial class StreamViewModel : ObservableObject, IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly ILogger<StreamViewModel> _logger;
    private readonly IGvspStreamSession _streamSession;
    private readonly IGvspDisplayConverter _displayConverter;
    private readonly DispatcherTimer _statusTimer;
    private WriteableBitmap? _bitmap;
    private RenderRequest? _pendingRenderRequest;
    private int _renderQueued;
    private int _streamGeneration;

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

    public StreamViewModel(
        Dispatcher dispatcher,
        ILogger<StreamViewModel> logger,
        IGvspStreamSession streamSession,
        IGvspDisplayConverter displayConverter)
    {
        _dispatcher = dispatcher;
        _logger = logger;
        _streamSession = streamSession;
        _displayConverter = displayConverter;
        _streamSession.FrameReceived += OnFrameReceived;
        _streamSession.PacketStatsUpdated += OnPacketStatsUpdated;
        _statusTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(500),
        };
        _statusTimer.Tick += (_, _) => RefreshStatusText();
    }

    public int StartStreaming(int streamPort = 0)
    {
        if (IsStreaming)
        {
            _logger.LogWarning("StartStreaming called while already streaming");
            return 0;
        }

        try
        {
            var localPort = _streamSession.Start(streamPort);
            if (localPort == 0)
                return 0;

            IsStreaming = true;
            Fps = 0;
            UdpPacketCount = 0;
            ReceivedFrames = 0;
            PacketSummary = string.Empty;
            ImageStats = string.Empty;
            _frameCount = 0;
            _receivedFrameCount = 0;
            _completedFrameCount = 0;
            Interlocked.Increment(ref _streamGeneration);
            _lastFpsTime = DateTime.UtcNow;
            StatusText = "Streaming...";
            _statusTimer.Start();

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

    private void OnFrameReceived(object? sender, GvspFrame frame)
    {
        Interlocked.Increment(ref _completedFrameCount);
        Interlocked.Exchange(ref _pendingRenderRequest, new RenderRequest(frame, Volatile.Read(ref _streamGeneration)));
        QueueRender();
    }

    private void QueueRender()
    {
        if (Interlocked.Exchange(ref _renderQueued, 1) != 0)
            return;

        _ = Task.Run(ProcessRenderQueueAsync);
    }

    private async Task ProcessRenderQueueAsync()
    {
        while (true)
        {
            var request = Interlocked.Exchange(ref _pendingRenderRequest, null);
            if (request is not null &&
                request.Generation == Volatile.Read(ref _streamGeneration) &&
                _displayConverter.TryConvert(request.Frame, out var displayFrame))
            {
                await _dispatcher.InvokeAsync(() => PresentFrame(request.Frame, displayFrame, request.Generation));
            }

            if (Volatile.Read(ref _pendingRenderRequest) is null)
            {
                Interlocked.Exchange(ref _renderQueued, 0);
                if (Volatile.Read(ref _pendingRenderRequest) is null ||
                    Interlocked.Exchange(ref _renderQueued, 1) != 0)
                {
                    return;
                }
            }
        }
    }

    private void OnPacketStatsUpdated(object? sender, GvspPacketStats stats)
    {
        _dispatcher.BeginInvoke(() =>
        {
            UdpPacketCount = stats.ReceivedPacketCount;
            PacketSummary = stats.Summary;
            RefreshStatusText();
        });
    }

    private void PresentFrame(GvspFrame frame, GvspDisplayFrame displayFrame, int generation)
    {
        if (!IsStreaming || generation != Volatile.Read(ref _streamGeneration))
            return;

        var pixelFormat = ToWpfPixelFormat(displayFrame.DisplayFormat);
        if (_bitmap is null ||
            _bitmap.PixelWidth != displayFrame.Width ||
            _bitmap.PixelHeight != displayFrame.Height ||
            _bitmap.Format != pixelFormat)
        {
            _logger.LogInformation(
                "Creating bitmap {Width}x{Height} ({FormatName}, PixelFormat=0x{PixelFormat:X8})",
                displayFrame.Width,
                displayFrame.Height,
                displayFrame.FormatName,
                frame.PixelFormat);
            Bitmap = new WriteableBitmap(displayFrame.Width, displayFrame.Height, 96, 96, pixelFormat, null);
        }

        _bitmap!.WritePixels(
            new Int32Rect(0, 0, displayFrame.Width, displayFrame.Height),
            displayFrame.Data,
            displayFrame.Stride,
            0);

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

        var stats = _streamSession.PacketStats;
        Resolution = $"{displayFrame.Width} x {displayFrame.Height} {displayFrame.FormatName}";
        ImageStats = displayFrame.ImageStats;
        UdpPacketCount = stats.ReceivedPacketCount;
        PacketSummary = stats.Summary;
        RefreshStatusText();
    }

    public void StopStreaming()
    {
        if (!IsStreaming)
            return;

        _streamSession.Stop();
        _statusTimer.Stop();
        Interlocked.Increment(ref _streamGeneration);
        Interlocked.Exchange(ref _pendingRenderRequest, null);
        IsStreaming = false;
        StatusText = "Stopped";
        StopCommand.NotifyCanExecuteChanged();
        StartCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        _streamSession.FrameReceived -= OnFrameReceived;
        _streamSession.PacketStatsUpdated -= OnPacketStatsUpdated;
        _statusTimer.Stop();
        _streamSession.Dispose();
    }

    private sealed record RenderRequest(GvspFrame Frame, int Generation);

    private void RefreshStatusText()
    {
        if (!IsStreaming)
            return;

        var stats = _streamSession.PacketStats;
        UdpPacketCount = stats.ReceivedPacketCount;
        PacketSummary = stats.Summary;
        var completed = Volatile.Read(ref _completedFrameCount);

        if (string.IsNullOrEmpty(Resolution))
        {
            StatusText =
                $"Receiving UDP ({UdpPacketCount} packets; {PacketSummary}), completed {completed} frame(s), waiting for render...";
            return;
        }

        StatusText = $"{Fps:F1} fps  |  {Resolution}  |  rendered {ReceivedFrames}, completed {completed}  |  {ImageStats}  |  {UdpPacketCount} packets  |  {PacketSummary}";
    }

    private static PixelFormat ToWpfPixelFormat(DisplayPixelFormat format) => format switch
    {
        DisplayPixelFormat.Gray8 => PixelFormats.Gray8,
        DisplayPixelFormat.Bgr24 => PixelFormats.Bgr24,
        DisplayPixelFormat.Bgr32 => PixelFormats.Bgr32,
        _ => PixelFormats.Gray8,
    };
}
