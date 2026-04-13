using System.Buffers.Binary;
using GenICam.Net.GenApi;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
    private readonly ILogger<GigEPort> _logger;

    /// <summary>
    /// Creates a new GigE Vision port backed by the given GVCP client.
    /// </summary>
    /// <param name="client">The GVCP client to use for register access.</param>
    /// <param name="logger">Optional logger instance.</param>
    public GigEPort(GvcpClient client, ILogger<GigEPort>? logger = null)
    {
        _client = client;
        _logger = logger ?? NullLogger<GigEPort>.Instance;
    }

    /// <inheritdoc/>
    public byte[] Read(long address, long length)
    {
        if (length <= 0)
            return [];

        _logger.LogDebug("Port.Read 0x{Address:X8}, length={Length}", address, length);
        var result = new byte[length];
        var offset = 0;
        var remaining = (int)length;

        while (remaining > 0)
        {
            var chunkSize = Math.Min(remaining, GvcpConstants.MaxBlockSize);
            var addr = (uint)(address + offset);
            var chunk = Task.Run(() => _client.ReadMemoryAsync(addr, chunkSize)).GetAwaiter().GetResult();
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

        _logger.LogDebug("Port.Write 0x{Address:X8}, {Length} bytes", address, data.Length);
        var offset = 0;
        var remaining = data.Length;

        while (remaining > 0)
        {
            var chunkSize = Math.Min(remaining, GvcpConstants.MaxBlockSize);
            var chunk = new byte[chunkSize];
            Array.Copy(data, offset, chunk, 0, chunkSize);
            var addr = (uint)(address + offset);
            Task.Run(() => _client.WriteMemoryAsync(addr, chunk)).GetAwaiter().GetResult();
            offset += chunkSize;
            remaining -= chunkSize;
        }
    }
}
