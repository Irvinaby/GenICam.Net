namespace GenICam.Net.GenApi;

/// <summary>
/// Extends <see cref="INode"/> with a generic string-based value accessor and change notification.
/// All value-carrying node types (Integer, Float, Boolean, String, Enumeration) implement this interface.
/// </summary>
/// <remarks>
/// Use <see cref="ValueAsString"/> for type-agnostic value access, or cast to a specific interface
/// (e.g., <see cref="IInteger"/>) for strongly-typed access.
/// </remarks>
public interface IValue : INode
{
    /// <summary>
    /// Gets or sets the node value as a culture-invariant string representation.
    /// For integers this is a decimal string, for floats a round-trip formatted string,
    /// for booleans "true"/"false", and for enumerations the symbolic entry name.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the parsed value is outside the valid range.</exception>
    /// <exception cref="FormatException">Thrown if the string cannot be parsed to the underlying type.</exception>
    string ValueAsString { get; set; }

    /// <summary>
    /// Raised whenever the node's value changes, either from direct assignment or register update.
    /// Subscribe to this event to keep UI elements or dependent logic in sync.
    /// </summary>
    event EventHandler? ValueChanged;
}
