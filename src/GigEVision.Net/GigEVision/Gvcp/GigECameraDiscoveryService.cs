using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GenICam.Net.GigEVision.Gvcp;

/// <summary>
/// Application-level discovery facade for GigE Vision cameras.
/// </summary>
public sealed class GigECameraDiscoveryService
{
    private readonly ILogger<GigECameraDiscoveryService> _logger;

    public GigECameraDiscoveryService(ILogger<GigECameraDiscoveryService>? logger = null)
    {
        _logger = logger ?? NullLogger<GigECameraDiscoveryService>.Instance;
    }

    public async Task<IReadOnlyList<GigECameraInfo>> DiscoverAsync(
        int timeoutMs = 3000,
        CancellationToken cancellationToken = default)
    {
        using var transport = new UdpTransportAdapter();
        using var discovery = new GigEDiscovery(transport);
        var cameras = await discovery.DiscoverAsync(timeoutMs: timeoutMs, cancellationToken: cancellationToken);

        foreach (var cam in cameras)
        {
            _logger.LogInformation(
                "Discovered camera: {Vendor} {Model} at {IpAddress}",
                cam.ManufacturerName,
                cam.ModelName,
                cam.IpAddress);
        }

        return cameras;
    }
}
