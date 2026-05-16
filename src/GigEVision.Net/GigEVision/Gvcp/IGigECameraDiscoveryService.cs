namespace GenICam.Net.GigEVision.Gvcp;

/// <summary>
/// Discovers GigE Vision cameras visible on the local network.
/// </summary>
public interface IGigECameraDiscoveryService
{
    Task<IReadOnlyList<GigECameraInfo>> DiscoverAsync(
        int timeoutMs = 3000,
        CancellationToken cancellationToken = default);
}
