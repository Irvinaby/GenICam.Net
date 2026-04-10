namespace GenICam.Net.GenApi;

/// <summary>
/// Represents a double-precision floating-point feature as defined by the GenICam GenApi standard.
/// </summary>
/// <remarks>
/// Common examples: ExposureTime, Gain, FrameRate, DeviceTemperature.
/// The value must satisfy: <c>Min &lt;= Value &lt;= Max</c>.
/// </remarks>
public interface IFloat : IValue
{
    /// <summary>
    /// Gets or sets the floating-point value.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when value is outside [<see cref="Min"/>, <see cref="Max"/>].</exception>
    double Value { get; set; }

    /// <summary>Minimum allowed value (inclusive).</summary>
    double Min { get; }

    /// <summary>Maximum allowed value (inclusive).</summary>
    double Max { get; }

    /// <summary>Whether a discrete increment is defined for this node.</summary>
    bool HasIncrement { get; }

    /// <summary>Step size for discrete float values. Only meaningful when <see cref="HasIncrement"/> is <c>true</c>.</summary>
    double Increment { get; }

    /// <summary>Display hint indicating how the value should be presented.</summary>
    Representation Representation { get; }

    /// <summary>Physical unit string (e.g., "us", "dB", "Hz"). Empty if dimensionless.</summary>
    string Unit { get; }

    /// <summary>Gets or sets the value truncated to a 64-bit integer. Useful for register-level interop.</summary>
    long IntValue { get; set; }
}
