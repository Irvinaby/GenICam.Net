namespace GenICam.Net.GigEVision.Gvsp;

/// <summary>
/// UI-neutral image buffer ready to copy into a display surface.
/// </summary>
public sealed record GvspDisplayFrame(
    int Width,
    int Height,
    DisplayPixelFormat DisplayFormat,
    int Stride,
    string FormatName,
    string ImageStats,
    byte[] Data);
