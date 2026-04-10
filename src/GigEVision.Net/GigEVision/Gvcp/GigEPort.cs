using System.Buffers.Binary;
using GenICam.Net.GenApi;

namespace GenICam.Net.GigEVision.Gvcp;

/// <summary>
/// <see cref="IPort"/> implementation that bridges the GenApi node tree to a GigE Vision camera
/// via GVCP register read/write commands. Automatically chunks large reads and writes
/// into blocks of <see cref="GvcpConstants.MaxBlockSize"/> bytes.
/// </summary>
/// <remarks>
/// <para><b>Usage:</b></para>
/// <code>
/// var transport = new UdpTransportAdapter(new UdpClient());
/// var client = new GvcpClient(transport, new IPEndPoint(cameraIp, 3956));
/// var port = new GigEPort(client);
/// nodeMap.Connect(port); // wire all register nodes to GigE Vision
/// </code>
/// </remarks>
public class GigEPort : IPort
{
    private readonly GvcpClient _client;

    /// <summary>
    /// Creates a new GigE Vision port backed by the given GVCP client.
    /// </summary>
    /// <param name="client">The GVCP client to use for register access.</param>
    public GigEPort(GvcpClient client)
    {
        _client = client;
    }

    /// <inheritdoc/>
    public byte[] Read(long address, long length)
    {
        if (length <= 0)
            return [];

        var result = new byte[length];
        var offset = 0;
        var remaining = (int)length;

        while (remaining > 0)
        {
            var chunkSize = Math.Min(remaining, GvcpConstants.MaxBlockSize);
            var chunk = _client.ReadMemoryAsync((uint)(address + offset), chunkSize).GetAwaiter().GetResult();
            chunk.CopyTo(result, offset);
            offset += chunkSize;
            remaining -= chunkSize;
        }

        return result;
    }

    /// <inheritdoc/>
    public void Write(long address, byte[] data)
    {
        if (data.Length == 0)
            return;

        var offset = 0;
        var remaining = data.Length;

        while (remaining > 0)
        {
            var chunkSize = Math.Min(remaining, GvcpConstants.MaxBlockSize);
            var chunk = new byte[chunkSize];
            Array.Copy(data, offset, chunk, 0, chunkSize);
            _client.WriteMemoryAsync((uint)(address + offset), chunk).GetAwaiter().GetResult();
            offset += chunkSize;
            remaining -= chunkSize;
        }
    }
}
