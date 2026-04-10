namespace GenICam.Net.GenApi;

/// <summary>
/// Represents a boolean feature as defined by the GenICam GenApi standard.
/// </summary>
/// <remarks>
/// Common examples: ReverseX, ReverseY, AcquisitionFrameRateEnable.
/// In XML, boolean values are represented as "true"/"false" or "1"/"0".
/// </remarks>
public interface IBoolean : IValue
{
    /// <summary>Gets or sets the boolean value.</summary>
    bool Value { get; set; }
}
