namespace GenICam.Net.GigEVision.Gvsp;

/// <summary>
/// Owns a GVSP receive session and publishes decoded frames.
/// </summary>
public interface IGvspStreamSession : IDisposable
{
    event EventHandler<GvspFrame>? FrameReceived;

    event EventHandler<GvspPacketStats>? PacketStatsUpdated;

    bool IsStreaming { get; }

    int LocalPort { get; }

    GvspPacketStats PacketStats { get; }

    int Start(int streamPort = 0);

    void Stop();
}
