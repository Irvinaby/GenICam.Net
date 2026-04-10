namespace GenICam.Net.GenApi;

/// <summary>
/// Represents a string feature as defined by the GenICam GenApi standard.
/// </summary>
/// <remarks>
/// Common examples: DeviceUserID, DeviceFirmwareVersion, DeviceSerialNumber.
/// </remarks>
public interface IString : IValue
{
    /// <summary>
    /// Gets or sets the string value.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the string length exceeds <see cref="MaxLength"/>.</exception>
    string Value { get; set; }

    /// <summary>Maximum allowed string length in characters. 0 means unlimited.</summary>
    long MaxLength { get; }
}
