using System.Net;

namespace GenICam.Net.GigEVision.Gvcp;

/// <summary>
/// GigE Vision Control Protocol client. Sends GVCP commands over UDP and parses acknowledgments.
/// Uses <see cref="IUdpTransport"/> for socket operations to enable testability.
/// </summary>
/// <remarks>
/// <para><b>Example:</b></para>
/// <code>
/// using var client = new GvcpClient(transport, cameraEndPoint);
/// uint value = await client.ReadRegisterAsync(0x0000);
/// await client.WriteRegisterAsync(0x0004, 0x00000001);
/// byte[] data = await client.ReadMemoryAsync(0x1000, 256);
/// </code>
/// </remarks>
public class GvcpClient : IDisposable
{
    private readonly IUdpTransport _transport;
    private readonly IPEndPoint _remoteEndPoint;
    private readonly int _timeoutMs;
    private ushort _requestId;

    /// <summary>
    /// Creates a new GVCP client connected to the specified camera endpoint.
    /// </summary>
    /// <param name="transport">UDP transport implementation.</param>
    /// <param name="remoteEndPoint">Camera IP endpoint (IP:3956).</param>
    /// <param name="timeoutMs">Timeout for ACK responses in milliseconds.</param>
    public GvcpClient(IUdpTransport transport, IPEndPoint remoteEndPoint, int timeoutMs = GvcpConstants.DefaultTimeoutMs)
    {
        _transport = transport;
        _remoteEndPoint = remoteEndPoint;
        _timeoutMs = timeoutMs;
    }

    /// <summary>
    /// Reads a single 32-bit register value.
    /// </summary>
    /// <param name="address">Register address.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The 32-bit register value.</returns>
    /// <exception cref="GvcpException">Thrown when the device returns a non-success status.</exception>
    /// <exception cref="TimeoutException">Thrown when no ACK is received within the timeout.</exception>
    public async Task<uint> ReadRegisterAsync(uint address, CancellationToken cancellationToken = default)
    {
        var reqId = NextRequestId();
        var packet = GvcpPackets.BuildReadRegCmd(reqId, address);

        var response = await SendAndReceiveAsync(packet, cancellationToken);
        var ackHeader = GvcpAckHeader.FromBytes(response);

        ThrowIfError(ackHeader);

        var values = GvcpPackets.ParseReadRegAck(response);
        return values[0];
    }

    /// <summary>
    /// Writes a single 32-bit register value.
    /// </summary>
    /// <param name="address">Register address.</param>
    /// <param name="value">Value to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="GvcpException">Thrown when the device returns a non-success status.</exception>
    /// <exception cref="TimeoutException">Thrown when no ACK is received within the timeout.</exception>
    public async Task WriteRegisterAsync(uint address, uint value, CancellationToken cancellationToken = default)
    {
        var reqId = NextRequestId();
        var packet = GvcpPackets.BuildWriteRegCmd(reqId, (address, value));

        var response = await SendAndReceiveAsync(packet, cancellationToken);
        var ackHeader = GvcpAckHeader.FromBytes(response);

        ThrowIfError(ackHeader);
    }

    /// <summary>
    /// Reads a block of memory from the device.
    /// </summary>
    /// <param name="address">Start address.</param>
    /// <param name="length">Number of bytes to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The memory contents.</returns>
    /// <exception cref="GvcpException">Thrown when the device returns a non-success status.</exception>
    /// <exception cref="TimeoutException">Thrown when no ACK is received within the timeout.</exception>
    public async Task<byte[]> ReadMemoryAsync(uint address, int length, CancellationToken cancellationToken = default)
    {
        var reqId = NextRequestId();
        var packet = GvcpPackets.BuildReadMemCmd(reqId, address, (ushort)length);

        var response = await SendAndReceiveAsync(packet, cancellationToken);
        var ackHeader = GvcpAckHeader.FromBytes(response);

        ThrowIfError(ackHeader);

        var (_, data) = GvcpPackets.ParseReadMemAck(response);
        return data;
    }

    /// <summary>
    /// Writes a block of memory to the device.
    /// </summary>
    /// <param name="address">Start address.</param>
    /// <param name="data">Data to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="GvcpException">Thrown when the device returns a non-success status.</exception>
    /// <exception cref="TimeoutException">Thrown when no ACK is received within the timeout.</exception>
    public async Task WriteMemoryAsync(uint address, byte[] data, CancellationToken cancellationToken = default)
    {
        var reqId = NextRequestId();
        var packet = GvcpPackets.BuildWriteMemCmd(reqId, address, data);

        var response = await SendAndReceiveAsync(packet, cancellationToken);
        var ackHeader = GvcpAckHeader.FromBytes(response);

        ThrowIfError(ackHeader);
    }

    private async Task<byte[]> SendAndReceiveAsync(byte[] packet, CancellationToken cancellationToken)
    {
        await _transport.SendAsync(packet, _remoteEndPoint, cancellationToken);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_timeoutMs);

        try
        {
            var result = await _transport.ReceiveAsync(cts.Token);
            return result.Buffer;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"No GVCP response received within {_timeoutMs}ms.");
        }
    }

    private ushort NextRequestId() => ++_requestId;

    private static void ThrowIfError(GvcpAckHeader ackHeader)
    {
        if (ackHeader.Status != GvcpStatus.Success)
            throw new GvcpException(ackHeader.Status);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _transport.Dispose();
        GC.SuppressFinalize(this);
    }
}
