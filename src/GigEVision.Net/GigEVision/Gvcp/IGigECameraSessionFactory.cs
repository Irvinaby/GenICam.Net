namespace GenICam.Net.GigEVision.Gvcp;

/// <summary>
/// Creates connected GigE Vision camera sessions.
/// </summary>
public interface IGigECameraSessionFactory
{
    Task<IGigECameraSession> ConnectAsync(
        GigECameraInfo camera,
        string? xmlSaveDirectory = null,
        CancellationToken cancellationToken = default);
}
