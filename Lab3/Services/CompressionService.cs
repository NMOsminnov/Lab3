using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Lab3.Services;

public static class CompressionService
{
    private const string MAGIC_HEADER = "ZC:";
    private static readonly ILogService _log = App.Services?.GetService<ILogService>()!;

    public static byte[] Compress(byte[] data)
    {
        _log?.LogDebug($"[ZIP] Compress: input={data.Length} bytes");

        using var output = new MemoryStream();
        var headerBytes = Encoding.UTF8.GetBytes(MAGIC_HEADER);
        output.Write(headerBytes, 0, headerBytes.Length);

        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            var entry = zip.CreateEntry("d", CompressionLevel.Optimal);
            using var entryStream = entry.Open();
            entryStream.Write(data, 0, data.Length);
        }

        var result = output.ToArray();
        _log?.LogDebug($"[ZIP] Compress complete: {data.Length} → {result.Length} bytes");
        return result;
    }

    public static byte[] Decompress(byte[] compressed)
    {
        _log?.LogDebug($"[ZIP] Decompress: input={compressed.Length} bytes");

        var headerBytes = Encoding.UTF8.GetBytes(MAGIC_HEADER);
        if (compressed.Length < headerBytes.Length)
            throw new InvalidDataException("Data too short");

        for (int i = 0; i < headerBytes.Length; i++)
        {
            if (compressed[i] != headerBytes[i])
                throw new InvalidDataException("Invalid header");
        }

        using var input = new MemoryStream(compressed, headerBytes.Length, compressed.Length - headerBytes.Length);
        using var zip = new ZipArchive(input, ZipArchiveMode.Read);
        var entry = zip.Entries[0];

        using var entryStream = entry.Open();
        using var output = new MemoryStream();
        entryStream.CopyTo(output);

        var result = output.ToArray();
        _log?.LogDebug($"[ZIP] Decompress complete: {compressed.Length} → {result.Length} bytes");
        return result;
    }

    public static byte[] TryDecompress(byte[] data, out bool wasCompressed)
    {
        wasCompressed = false;
        try
        {
            var result = Decompress(data);
            wasCompressed = true;
            return result;
        }
        catch (Exception ex)
        {
            _log?.LogDebug($"[ZIP] TryDecompress failed: {ex.Message}");
            return data;
        }
    }
}