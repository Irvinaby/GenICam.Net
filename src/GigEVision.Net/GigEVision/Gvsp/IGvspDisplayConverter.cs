namespace GenICam.Net.GigEVision.Gvsp;

/// <summary>
/// Converts GVSP frames into display-ready pixel buffers.
/// </summary>
public interface IGvspDisplayConverter
{
    bool TryConvert(GvspFrame frame, out GvspDisplayFrame displayFrame);
}
