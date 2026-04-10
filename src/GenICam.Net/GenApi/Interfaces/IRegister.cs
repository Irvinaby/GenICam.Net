namespace GenICam.Net.GenApi;

/// <summary>
/// Provides raw byte-level access to a device register as defined by the GenICam GenApi standard.
/// Registers are the low-level building blocks that back higher-level features (Integer, Float, etc.).
/// </summary>
/// <remarks>
/// Most applications should use typed interfaces (<see cref="IInteger"/>, <see cref="IFloat"/>, etc.)
/// rather than raw register access. Use <see cref="IRegister"/> for advanced scenarios such as
/// reading firmware blocks or vendor-specific data.
/// </remarks>
public interface IRegister : INode
{
    /// <summary>
    /// Reads raw bytes from the register.
    /// </summary>
    /// <param name="length">Number of bytes to read.</param>
    /// <returns>A byte array containing the register data.</returns>
    byte[] Get(long length);

    /// <summary>
    /// Writes raw bytes to the register.
    /// </summary>
    /// <param name="data">The bytes to write. Length must not exceed <see cref="Length"/>.</param>
    void Set(byte[] data);

    /// <summary>Base address of the register in the device memory map.</summary>
    long Address { get; }

    /// <summary>Size of the register in bytes.</summary>
    long Length { get; }
}
