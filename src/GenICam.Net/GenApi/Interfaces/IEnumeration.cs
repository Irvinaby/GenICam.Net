using System.Collections.ObjectModel;

namespace GenICam.Net.GenApi;

/// <summary>
/// Represents an enumeration feature as defined by the GenICam GenApi standard.
/// An enumeration maps symbolic names to integer values.
/// </summary>
/// <remarks>
/// Common examples: PixelFormat, TriggerMode, TriggerSource, AcquisitionMode.
/// Values can be accessed either by symbolic name (<see cref="Value"/>) or
/// numeric value (<see cref="IntValue"/>).
/// </remarks>
public interface IEnumeration : IValue
{
    /// <summary>
    /// Gets or sets the current entry by its symbolic name (e.g., "Mono8").
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the symbolic name does not match any entry.</exception>
    string Value { get; set; }

    /// <summary>
    /// Gets or sets the current entry by its numeric value.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when no entry has the given numeric value.</exception>
    long IntValue { get; set; }

    /// <summary>Read-only list of all available enumeration entries.</summary>
    ReadOnlyCollection<IEnumEntry> Entries { get; }

    /// <summary>
    /// Looks up an entry by its symbolic name.
    /// </summary>
    /// <param name="name">The symbolic name to search for.</param>
    /// <returns>The matching entry, or <c>null</c> if not found.</returns>
    IEnumEntry? GetEntryByName(string name);
}
