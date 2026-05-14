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
                var urlBytes = await ReadMemoryInChunksAsync(client, address, GvcpConstants.UrlRegisterLength, cancellationToken);
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
            return await LoadLocalNodeMapAsync(client, url, logger, cancellationToken);

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
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

        logger?.LogInformation("Reading local camera XML: file={Filename}, address=0x{Address:X}, size={Size}",
            filename, xmlAddress, xmlSize);

        var xmlData = await ReadMemoryInChunksAsync(client, xmlAddress, xmlSize, cancellationToken);
        return ParseCameraXml(filename, xmlData, logger);
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
        CancellationToken cancellationToken)
    {
        var result = new byte[length];
        var offset = 0;

        while (offset < length)
        {
            var chunkSize = Math.Min(length - offset, GvcpConstants.MaxBlockSize);
            var chunk = await client.ReadMemoryAsync(address + (uint)offset, chunkSize, cancellationToken);
            chunk.CopyTo(result, offset);
            offset += chunkSize;
        }

        return result;
    }

    private static NodeMap ParseCameraXml(string filename, byte[] xmlData, ILogger? logger)
    {
        string xmlContent;
        if (filename.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
            (xmlData.Length >= 2 && xmlData[0] == 0x50 && xmlData[1] == 0x4B))
        {
            using var memStream = new MemoryStream(xmlData);
            using var archive = new ZipArchive(memStream, ZipArchiveMode.Read);
            var entry = archive.Entries.FirstOrDefault(item => item.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                ?? archive.Entries.FirstOrDefault(item => item.Length > 0)
                ?? throw new InvalidDataException("Camera XML ZIP archive is empty.");

            using var reader = new StreamReader(entry.Open(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            xmlContent = reader.ReadToEnd();
            logger?.LogDebug("Decompressed XML from ZIP entry: {EntryName} ({Length} chars)", entry.Name, xmlContent.Length);
        }
        else
        {
            xmlContent = Encoding.UTF8.GetString(xmlData);
        }

        var nodeMap = NodeMapParser.Parse(xmlContent.TrimStart('\uFEFF', '\0').TrimEnd('\0'));
        logger?.LogInformation("Camera XML loaded: {NodeCount} nodes parsed", nodeMap.Nodes.Count);
        return nodeMap;
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

    private static uint ParseXmlUrlUInt32(string value)
    {
        value = value.Trim();
        return value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? Convert.ToUInt32(value[2..], 16)
            : Convert.ToUInt32(value, 16);
    }
}
