namespace GenICam.Net.GenApi;

/// <summary>
/// Concrete register node for raw register access.
/// </summary>
public class RegisterNode : NodeBase, IRegister
{
    private byte[] _cache = [];

    public long Address { get; internal set; }
    public long Length { get; internal set; }
    public Endianness Endianness { get; internal set; } = Endianness.LittleEndian;

    /// <summary>The port used for transport-layer access.</summary>
    internal IPort? Port { get; set; }

    public byte[] Get(long length)
    {
        if (Port != null)
        {
            _cache = Port.Read(Address, length);
        }
        return _cache;
    }

    public void Set(byte[] data)
    {
        _cache = data;
        Port?.Write(Address, data);
    }

    internal void SetCacheDirect(byte[] data) => _cache = data;
}
