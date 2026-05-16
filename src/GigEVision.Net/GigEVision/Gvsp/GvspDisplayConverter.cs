using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GenICam.Net.GigEVision.Gvsp;

/// <summary>
/// Converts GVSP image payloads into UI-neutral display buffers.
/// </summary>
public sealed class GvspDisplayConverter
{
    private readonly ILogger<GvspDisplayConverter> _logger;

    public GvspDisplayConverter(ILogger<GvspDisplayConverter>? logger = null)
    {
        _logger = logger ?? NullLogger<GvspDisplayConverter>.Instance;
    }

    public bool TryConvert(GvspFrame frame, out GvspDisplayFrame displayFrame)
    {
        var width = (int)frame.SizeX;
        var height = (int)frame.SizeY;
        displayFrame = new GvspDisplayFrame(0, 0, DisplayPixelFormat.Gray8, 0, string.Empty, string.Empty, []);

        if (width <= 0 || height <= 0)
        {
            _logger.LogWarning("Frame {FrameId} has invalid dimensions: {Width}x{Height}", frame.FrameId, width, height);
            return false;
        }

        if (!TryConvertForDisplay(frame, width, height, out var data, out var format, out var stride, out var formatName, out var stats))
            return false;

        displayFrame = new GvspDisplayFrame(width, height, format, stride, formatName, stats, data);
        return true;
    }

    private bool TryConvertForDisplay(
        GvspFrame frame,
        int width,
        int height,
        out byte[] displayData,
        out DisplayPixelFormat displayFormat,
        out int stride,
        out string formatName,
        out string imageStats)
    {
        displayFormat = DisplayPixelFormat.Gray8;
        stride = width;
        formatName = GetPixelFormatName(frame.PixelFormat);
        imageStats = GetByteStats(frame.Data);
        displayData = [];

        var pixelCount = checked(width * height);
        switch (frame.PixelFormat)
        {
            case 0x01080001:
            case 0x01080008:
            case 0x01080009:
            case 0x0108000A:
            case 0x0108000B:
                if (!HasEnoughData(frame, pixelCount, formatName))
                    return false;
                displayData = frame.Data;
                return true;
            case 0x01100003:
                return ConvertUnpackedMono(frame, pixelCount, 10, formatName, out displayData);
            case 0x01100005:
                return ConvertUnpackedMono(frame, pixelCount, 12, formatName, out displayData);
            case 0x01100007:
                return ConvertUnpackedMono(frame, pixelCount, 16, formatName, out displayData);
            case 0x010C0004:
                return ConvertMono10Packed(frame, pixelCount, formatName, out displayData);
            case 0x010C0006:
                return ConvertMono12Packed(frame, pixelCount, formatName, out displayData);
            case 0x02180014:
                displayFormat = DisplayPixelFormat.Bgr24;
                stride = width * 3;
                return ConvertRgb(frame, pixelCount, formatName, swapRedBlue: true, out displayData);
            case 0x02180015:
                displayFormat = DisplayPixelFormat.Bgr24;
                stride = width * 3;
                return ConvertRgb(frame, pixelCount, formatName, swapRedBlue: false, out displayData);
            case 0x02200016:
                displayFormat = DisplayPixelFormat.Bgr32;
                stride = width * 4;
                return ConvertRgba(frame, pixelCount, formatName, swapRedBlue: true, out displayData);
            case 0x02200017:
                displayFormat = DisplayPixelFormat.Bgr32;
                stride = width * 4;
                return ConvertRgba(frame, pixelCount, formatName, swapRedBlue: false, out displayData);
            default:
                if (!HasEnoughData(frame, pixelCount, formatName))
                    return false;
                _logger.LogWarning("Unsupported pixel format 0x{PixelFormat:X8}; displaying first byte per pixel as grayscale", frame.PixelFormat);
                displayData = frame.Data;
                return true;
        }
    }

    private bool ConvertUnpackedMono(GvspFrame frame, int pixelCount, int significantBits, string formatName, out byte[] displayData)
    {
        displayData = [];
        if (!HasEnoughData(frame, pixelCount * 2, formatName))
            return false;

        var values = new ushort[pixelCount];
        ushort min = ushort.MaxValue;
        ushort max = ushort.MinValue;
        for (var i = 0; i < pixelCount; i++)
        {
            var value = (ushort)(frame.Data[i * 2] | (frame.Data[i * 2 + 1] << 8));
            if (significantBits < 16)
                value = (ushort)Math.Min(value, (1 << significantBits) - 1);
            values[i] = value;
            min = Math.Min(min, value);
            max = Math.Max(max, value);
        }

        displayData = ScaleMono(values, min, max);
        return true;
    }

    private bool ConvertMono10Packed(GvspFrame frame, int pixelCount, string formatName, out byte[] displayData)
    {
        displayData = [];
        var expectedBytes = (pixelCount * 10 + 7) / 8;
        if (!HasEnoughData(frame, expectedBytes, formatName))
            return false;

        var values = new ushort[pixelCount];
        ushort min = ushort.MaxValue;
        ushort max = ushort.MinValue;
        var source = frame.Data;
        var src = 0;
        var dst = 0;
        while (dst < pixelCount)
        {
            var b0 = source[src++];
            var b1 = src < source.Length ? source[src++] : 0;
            var b2 = src < source.Length ? source[src++] : 0;
            var b3 = src < source.Length ? source[src++] : 0;
            var b4 = src < source.Length ? source[src++] : 0;
            AddPackedValue((ushort)(b0 | ((b1 & 0x03) << 8)));
            AddPackedValue((ushort)((b1 >> 2) | ((b2 & 0x0F) << 6)));
            AddPackedValue((ushort)((b2 >> 4) | ((b3 & 0x3F) << 4)));
            AddPackedValue((ushort)((b3 >> 6) | (b4 << 2)));
        }

        displayData = ScaleMono(values, min, max);
        return true;

        void AddPackedValue(ushort value)
        {
            if (dst >= pixelCount)
                return;
            values[dst++] = value;
            min = Math.Min(min, value);
            max = Math.Max(max, value);
        }
    }

    private bool ConvertMono12Packed(GvspFrame frame, int pixelCount, string formatName, out byte[] displayData)
    {
        displayData = [];
        var expectedBytes = (pixelCount * 12 + 7) / 8;
        if (!HasEnoughData(frame, expectedBytes, formatName))
            return false;

        var values = new ushort[pixelCount];
        ushort min = ushort.MaxValue;
        ushort max = ushort.MinValue;
        var source = frame.Data;
        var src = 0;
        var dst = 0;
        while (dst < pixelCount)
        {
            var b0 = source[src++];
            var b1 = src < source.Length ? source[src++] : 0;
            var b2 = src < source.Length ? source[src++] : 0;
            AddPackedValue((ushort)(b0 | ((b1 & 0x0F) << 8)));
            AddPackedValue((ushort)((b1 >> 4) | (b2 << 4)));
        }

        displayData = ScaleMono(values, min, max);
        return true;

        void AddPackedValue(ushort value)
        {
            if (dst >= pixelCount)
                return;
            values[dst++] = value;
            min = Math.Min(min, value);
            max = Math.Max(max, value);
        }
    }

    private bool ConvertRgb(GvspFrame frame, int pixelCount, string formatName, bool swapRedBlue, out byte[] displayData)
    {
        displayData = [];
        var expectedBytes = pixelCount * 3;
        if (!HasEnoughData(frame, expectedBytes, formatName))
            return false;

        displayData = new byte[expectedBytes];
        for (var src = 0; src < expectedBytes; src += 3)
        {
            displayData[src] = swapRedBlue ? frame.Data[src + 2] : frame.Data[src];
            displayData[src + 1] = frame.Data[src + 1];
            displayData[src + 2] = swapRedBlue ? frame.Data[src] : frame.Data[src + 2];
        }
        return true;
    }

    private bool ConvertRgba(GvspFrame frame, int pixelCount, string formatName, bool swapRedBlue, out byte[] displayData)
    {
        displayData = [];
        var expectedBytes = pixelCount * 4;
        if (!HasEnoughData(frame, expectedBytes, formatName))
            return false;

        displayData = new byte[expectedBytes];
        for (var src = 0; src < expectedBytes; src += 4)
        {
            displayData[src] = swapRedBlue ? frame.Data[src + 2] : frame.Data[src];
            displayData[src + 1] = frame.Data[src + 1];
            displayData[src + 2] = swapRedBlue ? frame.Data[src] : frame.Data[src + 2];
            displayData[src + 3] = 255;
        }
        return true;
    }

    private static byte[] ScaleMono(ushort[] values, ushort min, ushort max)
    {
        var displayData = new byte[values.Length];
        if (max <= min)
        {
            if (max > 0)
                Array.Fill(displayData, (byte)255);
            return displayData;
        }

        var scale = 255.0 / (max - min);
        for (var i = 0; i < values.Length; i++)
            displayData[i] = (byte)Math.Clamp((values[i] - min) * scale, 0, 255);

        return displayData;
    }

    private bool HasEnoughData(GvspFrame frame, int expectedBytes, string formatName)
    {
        if (frame.Data.Length >= expectedBytes)
            return true;

        _logger.LogWarning(
            "Frame {FrameId} {FormatName} data too short: {Actual} < {Expected} bytes",
            frame.FrameId,
            formatName,
            frame.Data.Length,
            expectedBytes);
        return false;
    }

    private static string GetPixelFormatName(uint pixelFormat) => pixelFormat switch
    {
        0x01080001 => "Mono8",
        0x01100003 => "Mono10",
        0x010C0004 => "Mono10Packed",
        0x01100005 => "Mono12",
        0x010C0006 => "Mono12Packed",
        0x01100007 => "Mono16",
        0x01080008 => "BayerGR8",
        0x01080009 => "BayerRG8",
        0x0108000A => "BayerGB8",
        0x0108000B => "BayerBG8",
        0x02180014 => "RGB8",
        0x02180015 => "BGR8",
        0x02200016 => "RGBA8",
        0x02200017 => "BGRA8",
        _ => $"0x{pixelFormat:X8}",
    };

    private static string GetByteStats(byte[] data)
    {
        if (data.Length == 0)
            return "empty payload";

        byte min = byte.MaxValue;
        byte max = byte.MinValue;
        long sum = 0;
        var samples = Math.Min(data.Length, 4096);
        var remainingSamples = samples;
        var step = Math.Max(1, data.Length / samples);
        for (var i = 0; i < data.Length && remainingSamples > 0; i += step, remainingSamples--)
        {
            var value = data[i];
            min = Math.Min(min, value);
            max = Math.Max(max, value);
            sum += value;
        }

        return $"raw min:{min} max:{max} avg:{sum / (double)samples:F1}";
    }
}
