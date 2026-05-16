using GenICam.Net.GenApi;

namespace GenICam.Net.GigEVision.Gvcp;

/// <summary>
/// Represents an active control session for a single GigE Vision camera.
/// </summary>
public interface IGigECameraSession : IDisposable
{
    GigECameraInfo Camera { get; }

    INodeMap NodeMap { get; }

    Task StartAcquisitionAsync(int localPort, CancellationToken cancellationToken = default);

    Task StopAcquisitionAsync(CancellationToken cancellationToken = default);
}
