using Microsoft.Extensions.Logging;

namespace GenICam.Net.GigEVision.Gvcp;

/// <summary>
/// Default factory for creating GigE Vision camera sessions.
/// </summary>
public sealed class GigECameraSessionFactory(ILogger<GigECameraSession>? logger = null) : IGigECameraSessionFactory
{
    public async Task<IGigECameraSession> ConnectAsync(
        GigECameraInfo camera,
        string? xmlSaveDirectory = null,
        CancellationToken cancellationToken = default)
        => await GigECameraSession.ConnectAsync(camera, xmlSaveDirectory, logger, cancellationToken);
}
