using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace Lab3.Services;

public static class StreamExtensions
{
    public static async Task<byte[]> ToByteArrayAsync(this IRandomAccessStream stream)
    {
        stream.Seek(0);
        using var reader = new DataReader(stream.GetInputStreamAt(0));
        await reader.LoadAsync((uint)stream.Size);
        var buffer = new byte[stream.Size];
        reader.ReadBytes(buffer);
        return buffer;
    }
}