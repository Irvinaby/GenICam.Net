namespace GenICam.Net.GenApi;

/// <summary>
/// Represents a 64-bit integer feature as defined by the GenICam GenApi standard.
/// Provides access to value, range, increment, and display hints.
/// </summary>
/// <remarks>
/// Common examples: image Width, Height, OffsetX, OffsetY, sensor pixel counts.
/// The value must satisfy: <c>Min &lt;= Value &lt;= Max</c> and <c>(Value - Min) % Increment == 0</c>.
/// </remarks>
public interface IInteger : IValue
{
    /// <summary>
    /// Gets or sets the integer value.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when value is outside [<see cref="Min"/>, <see cref="Max"/>].</exception>
    /// <exception cref="ArgumentException">Thrown when value does not align with <see cref="Increment"/>.</exception>
    long Value { get; set; }

    /// <summary>Minimum allowed value (inclusive).</summary>
    long Min { get; }

    /// <summary>Maximum allowed value (inclusive).</summary>
    long Max { get; }

    /// <summary>Step size between valid values. A value of 1 means any integer in range is valid.</summary>
    long Increment { get; }

    /// <summary>Display hint indicating how the value should be presented (linear slider, hex, etc.).</summary>
    Representation Representation { get; }

    /// <summary>Physical unit string (e.g., "px", "Hz", "us"). Empty if dimensionless.</summary>
    string Unit { get; }
}
