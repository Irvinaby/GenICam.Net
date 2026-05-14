using System.IO.Compression;
using System.Text;
using GenICam.Net.GenApi;
using Microsoft.Extensions.Logging;

namespace GenICam.Net.GigEVision.Gvcp;

/// <summary>
/// Loads a camera's GenICam XML description from the standard GigE Vision XML URL registers.
/// </summary>
public static class GvcpXmlLoader
{
    private const string XmlPathVariable = "GENICAM_XML_PATH";
    private const string XmlCachePathVariable = "GENICAM_XML_CACHE_PATH";

    /// <summary>
    /// Reads the first and second XML URL registers and returns the first successfully parsed node map.
    /// </summary>
    public static async Task<NodeMap> LoadNodeMapAsync(
        GvcpClient client,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var urls = await ReadCameraXmlUrlsAsync(client, logger, cancellationToken);
        logger?.LogInformation("Camera advertised {Count} XML URL(s): {Urls}", urls.Count, string.Join(", ", urls));

        foreach (var url in urls)
        {
            try
            {
                return await LoadNodeMapFromUrlAsync(client, url, logger, cancellationToken);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Could not load camera XML from URL {Url}; trying next URL if available", url);
            }
        }

        var cachedNodeMap = TryLoadCachedNodeMap(urls, logger);
        if (cachedNodeMap is not null)
            return cachedNodeMap;

        throw new InvalidOperationException("Failed to load camera XML from any advertised XML URL.");
    }

    internal static async Task<List<string>> ReadCameraXmlUrlsAsync(
        GvcpClient client,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var urls = new List<string>();
        foreach (var (name, address) in new[]
        {
            ("First URL", GvcpConstants.FirstUrlRegister),
            ("Second URL", GvcpConstants.SecondUrlRegister),
        })
        {
            try
            {
                var urlBytes = await ReadMemoryInChunksAsync(client, address, GvcpConstants.UrlRegisterLength, logger, cancellationToken);
                var url = DecodeBootstrapString(urlBytes);
                if (string.IsNullOrWhiteSpace(url))
                {
                    logger?.LogInformation("{Name} register is empty", name);
                    continue;
                }

                logger?.LogInformation("Camera XML {Name}: {Url}", name, url);
                urls.Add(url);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Could not read camera XML {Name} register", name);
            }
        }

        return urls.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static async Task<NodeMap> LoadNodeMapFromUrlAsync(
        GvcpClient client,
        string url,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        if (url.StartsWith("Local:", StringComparison.OrdinalIgnoreCase))
        {
            logger?.LogInformation("Trying local camera XML URL: {Url}", url);
            return await LoadLocalNodeMapAsync(client, url, logger, cancellationToken);
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            logger?.LogInformation("Trying HTTP camera XML URL: {Url}", url);
            return await LoadHttpNodeMapAsync(uri, logger, cancellationToken);
        }

        throw new NotSupportedException($"Unsupported camera XML URL scheme: {url}");
    }

    private static async Task<NodeMap> LoadLocalNodeMapAsync(
        GvcpClient client,
        string url,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        var parts = url["Local:".Length..].Split(';', StringSplitOptions.TrimEntries);
        if (parts.Length < 3)
            throw new FormatException($"Invalid local camera XML URL format: {url}");

        var filename = parts[0];
        var xmlAddress = ParseXmlUrlUInt32(parts[1]);
        var xmlSize = (int)ParseXmlUrlUInt32(parts[2]);

        logger?.LogInformation(
            "Parsed local camera XML descriptor: file={Filename}, address=0x{Address:X8}, size={Size}, rawAddress={RawAddress}, rawSize={RawSize}",
            filename,
            xmlAddress,
            xmlSize,
            parts[1],
            parts[2]);

        try
        {
            var xmlData = await ReadMemoryInChunksAsync(client, xmlAddress, xmlSize, logger, cancellationToken);
            logger?.LogInformation(
                "Read local camera XML bytes: file={Filename}, bytes={Length}, previewHex={PreviewHex}, previewAscii={PreviewAscii}",
                filename,
                xmlData.Length,
                FormatBytePreview(xmlData),
                FormatAsciiPreview(xmlData));

            return ParseCameraXml(filename, xmlData, logger);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Could not load local camera XML '{filename}' from 0x{xmlAddress:X8} ({xmlSize} bytes).",
                ex);
        }
    }

    private static async Task<NodeMap> LoadHttpNodeMapAsync(Uri uri, ILogger? logger, CancellationToken cancellationToken)
    {
        var candidates = uri.Scheme == Uri.UriSchemeHttp
            ? new[] { uri, new UriBuilder(uri) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri }
            : new[] { uri };

        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 GenICam.Net");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/xml");
        http.DefaultRequestHeaders.Accept.ParseAdd("text/xml");
        http.DefaultRequestHeaders.Accept.ParseAdd("*/*");

        Exception? lastException = null;
        foreach (var candidate in candidates)
        {
            try
            {
                logger?.LogInformation("Downloading camera XML from {Uri}", candidate);
                var xmlData = await http.GetByteArrayAsync(candidate, cancellationToken);
                return ParseCameraXml(Path.GetFileName(candidate.AbsolutePath), xmlData, logger);
            }
            catch (Exception ex)
            {
                lastException = ex;
                logger?.LogWarning(ex, "Could not download camera XML from {Uri}", candidate);
            }
        }

        throw new InvalidOperationException($"Could not download camera XML from {uri}.", lastException);
    }

    private static async Task<byte[]> ReadMemoryInChunksAsync(
        GvcpClient client,
        uint address,
        int length,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        var result = new byte[length];
        var offset = 0;
        logger?.LogInformation("Reading camera memory: address=0x{Address:X8}, length={Length}, maxChunk={MaxChunk}",
            address, length, GvcpConstants.MaxBlockSize);

        while (offset < length)
        {
            var remaining = length - offset;
            var chunkSize = Math.Min(remaining, GvcpConstants.MaxBlockSize);
            var requestedSize = AlignReadMemoryCount(chunkSize);
            var chunkAddress = address + (uint)offset;

            logger?.LogDebug(
                "Reading camera memory chunk: address=0x{Address:X8}, requested={Requested}, useful={Useful}, offset={Offset}",
                chunkAddress,
                requestedSize,
                chunkSize,
                offset);

            var chunk = await client.ReadMemoryAsync(chunkAddress, requestedSize, cancellationToken);
            if (chunk.Length != requestedSize)
            {
                logger?.LogWarning(
                    "Camera memory chunk length mismatch: address=0x{Address:X8}, requested={Requested}, useful={Useful}, received={Received}, offset={Offset}, previewHex={PreviewHex}, previewAscii={PreviewAscii}",
                    chunkAddress,
                    requestedSize,
                    chunkSize,
                    chunk.Length,
                    offset,
                    FormatBytePreview(chunk),
                    FormatAsciiPreview(chunk));

                throw new InvalidDataException(
                    $"Camera returned {chunk.Length} byte(s) for memory read at 0x{chunkAddress:X8}; expected {requestedSize}.");
            }

            chunk.AsSpan(0, chunkSize).CopyTo(result.AsSpan(offset));
            offset += chunkSize;
        }

        return result;
    }

    private static NodeMap ParseCameraXml(string filename, byte[] xmlData, ILogger? logger)
    {
        logger?.LogInformation(
            "Parsing camera XML payload: file={Filename}, bytes={Length}, looksLikeZip={LooksLikeZip}, previewHex={PreviewHex}, previewAscii={PreviewAscii}",
            filename,
            xmlData.Length,
            LooksLikeZip(filename, xmlData),
            FormatBytePreview(xmlData),
            FormatAsciiPreview(xmlData));

        string xmlContent;
        if (LooksLikeZip(filename, xmlData))
        {
            using var memStream = new MemoryStream(xmlData);
            using var archive = new ZipArchive(memStream, ZipArchiveMode.Read);
            logger?.LogInformation("Opened camera XML ZIP: file={Filename}, entries={Entries}",
                filename,
                string.Join(", ", archive.Entries.Select(entry => $"{entry.FullName} ({entry.Length} bytes)")));

            var entry = archive.Entries.FirstOrDefault(item => item.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                ?? archive.Entries.FirstOrDefault(item => item.Length > 0)
                ?? throw new InvalidDataException("Camera XML ZIP archive is empty.");

            using var reader = new StreamReader(entry.Open(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            xmlContent = reader.ReadToEnd();
            logger?.LogInformation("Decompressed XML from ZIP entry: {EntryName}, compressedBytes={CompressedLength}, xmlChars={Length}, preview={Preview}",
                entry.FullName,
                entry.CompressedLength,
                xmlContent.Length,
                FormatTextPreview(xmlContent));
        }
        else
        {
            xmlContent = Encoding.UTF8.GetString(xmlData);
            logger?.LogInformation("Decoded camera XML as UTF-8: file={Filename}, xmlChars={Length}, preview={Preview}",
                filename,
                xmlContent.Length,
                FormatTextPreview(xmlContent));
        }

        var trimmedXml = xmlContent.TrimStart('\uFEFF', '\0').TrimEnd('\0');
        try
        {
            var nodeMap = NodeMapParser.Parse(trimmedXml);
            logger?.LogInformation("Camera XML loaded: {NodeCount} nodes parsed", nodeMap.Nodes.Count);
            return nodeMap;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(
                ex,
                "Camera XML parse failed: file={Filename}, xmlChars={Length}, preview={Preview}",
                filename,
                trimmedXml.Length,
                FormatTextPreview(trimmedXml));
            throw;
        }
    }

    private static NodeMap? TryLoadCachedNodeMap(IReadOnlyCollection<string> urls, ILogger? logger)
    {
        var configuredXmlPath = Environment.GetEnvironmentVariable(XmlPathVariable);
        if (!string.IsNullOrWhiteSpace(configuredXmlPath) && File.Exists(configuredXmlPath))
        {
            logger?.LogInformation("Loading camera XML from {Variable}: {Path}", XmlPathVariable, configuredXmlPath);
            return ParseCameraXml(Path.GetFileName(configuredXmlPath), File.ReadAllBytes(configuredXmlPath), logger);
        }

        var candidateFileNames = GetCandidateFileNames(urls).ToList();
        if (candidateFileNames.Count == 0)
            return null;

        foreach (var directory in GetCacheDirectories())
        {
            if (!Directory.Exists(directory))
                continue;

            foreach (var fileName in candidateFileNames)
            {
                foreach (var path in EnumerateFilesSafely(directory, fileName))
                {
                    logger?.LogInformation("Loading camera XML from cache: {Path}", path);
                    return ParseCameraXml(Path.GetFileName(path), File.ReadAllBytes(path), logger);
                }
            }
        }

        logger?.LogInformation(
            "No cached camera XML found. Tried names [{Names}] in [{Directories}]",
            string.Join(", ", candidateFileNames),
            string.Join(", ", GetCacheDirectories()));
        return null;
    }

    private static IEnumerable<string> GetCandidateFileNames(IEnumerable<string> urls)
    {
        foreach (var url in urls)
        {
            string? fileName = null;
            if (url.StartsWith("Local:", StringComparison.OrdinalIgnoreCase))
                fileName = url["Local:".Length..].Split(';', StringSplitOptions.TrimEntries).FirstOrDefault();
            else if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                fileName = Path.GetFileName(uri.AbsolutePath);

            if (string.IsNullOrWhiteSpace(fileName))
                continue;

            yield return fileName;
            var extension = Path.GetExtension(fileName);
            if (extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
                yield return Path.ChangeExtension(fileName, ".xml");
            else if (extension.Equals(".xml", StringComparison.OrdinalIgnoreCase))
                yield return Path.ChangeExtension(fileName, ".zip");
        }
    }

    private static IEnumerable<string> GetCacheDirectories()
    {
        var configuredCachePath = Environment.GetEnvironmentVariable(XmlCachePathVariable);
        if (!string.IsNullOrWhiteSpace(configuredCachePath))
            yield return configuredCachePath;

        var configuredXmlPath = Environment.GetEnvironmentVariable(XmlPathVariable);
        if (!string.IsNullOrWhiteSpace(configuredXmlPath) && Directory.Exists(configuredXmlPath))
            yield return configuredXmlPath;

        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (!string.IsNullOrWhiteSpace(programData))
        {
            yield return Path.Combine(programData, "Spinnaker", "XML");
            yield return Path.Combine(programData, "Spinnaker", "Shared", "XML");
        }

        var publicDocuments = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments);
        if (!string.IsNullOrWhiteSpace(publicDocuments))
            yield return Path.Combine(publicDocuments, "National Instruments", "NI-IMAQdx", "Data", "XML");
    }

    private static IEnumerable<string> EnumerateFilesSafely(string directory, string fileName)
    {
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(directory, fileName, SearchOption.AllDirectories);
        }
        catch
        {
            yield break;
        }

        foreach (var file in files)
            yield return file;
    }

    private static string DecodeBootstrapString(byte[] bytes)
    {
        var length = Array.IndexOf(bytes, (byte)0);
        if (length < 0)
            length = bytes.Length;

        return Encoding.ASCII.GetString(bytes, 0, length).Trim();
    }

    private static bool LooksLikeZip(string filename, byte[] data)
        => filename.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
           (data.Length >= 2 && data[0] == 0x50 && data[1] == 0x4B);

    private static int AlignReadMemoryCount(int count)
    {
        const int alignment = 4;
        return count % alignment == 0
            ? count
            : count + alignment - count % alignment;
    }

    private static string FormatBytePreview(byte[] data)
    {
        const int previewLength = 32;
        return data.Length == 0
            ? "<empty>"
            : Convert.ToHexString(data.AsSpan(0, Math.Min(previewLength, data.Length)));
    }

    private static string FormatAsciiPreview(byte[] data)
    {
        const int previewLength = 64;
        if (data.Length == 0)
            return "<empty>";

        var chars = data
            .Take(previewLength)
            .Select(value => value is >= 0x20 and <= 0x7E ? (char)value : '.')
            .ToArray();

        return new string(chars);
    }

    private static string FormatTextPreview(string text)
    {
        const int previewLength = 160;
        if (string.IsNullOrEmpty(text))
            return "<empty>";

        var preview = text
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('\t', ' ');

        return preview.Length <= previewLength ? preview : preview[..previewLength];
    }

    private static uint ParseXmlUrlUInt32(string value)
    {
        value = value.Trim();
        return value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? Convert.ToUInt32(value[2..], 16)
            : Convert.ToUInt32(value, 16);
    }
}
