using System.Collections.ObjectModel;

namespace GenICam.Net.GenApi;

/// <summary>
/// Represents a category node that groups related features for UI presentation.
/// Categories form the top-level structure of a camera's feature tree.
/// </summary>
/// <remarks>
/// Common examples: "ImageFormatControl" (groups Width, Height, PixelFormat),
/// "AcquisitionControl" (groups ExposureTime, FrameRate, TriggerMode).
/// Categories may be nested.
/// </remarks>
public interface ICategory : INode
{
    /// <summary>Ordered list of child feature nodes belonging to this category.</summary>
    ReadOnlyCollection<INode> Features { get; }
}
