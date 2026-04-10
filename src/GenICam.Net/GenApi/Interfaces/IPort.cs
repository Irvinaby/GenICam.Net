namespace GenICam.Net.GenApi;

/// <summary>
/// Transport layer port interface for reading and writing device registers.
/// Implement this interface to connect a GenICam node map to a physical device
/// via a specific transport (GigE Vision, USB3 Vision, etc.).
/// </summary>
/// <remarks>
/// This is the bridge between the GenApi node tree and the hardware.
/// After parsing an XML description into an <see cref="INodeMap"/>, call
/// <see cref="INodeMap.Connect(IPort)"/> to wire up register access.
/// <para>
/// <b>Example:</b> A GigE Vision transport would implement <see cref="Read"/> and <see cref="Write"/>
/// using GVCP ReadMem/WriteMem commands over UDP.
/// </para>
/// </remarks>
public interface IPort
{
    /// <summary>
    /// Reads a block of bytes from the device at the given memory address.
    /// </summary>
    /// <param name="address">The byte address in the device's register map.</param>
    /// <param name="length">The number of bytes to read.</param>
    /// <returns>A byte array of the requested length containing the register data.</returns>
    byte[] Read(long address, long length);

    /// <summary>
    /// Writes a block of bytes to the device at the given memory address.
    /// </summary>
    /// <param name="address">The byte address in the device's register map.</param>
    /// <param name="data">The bytes to write.</param>
    void Write(long address, byte[] data);
}
