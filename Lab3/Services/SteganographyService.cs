using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Graphics.Imaging;
using Lab3.Models;

namespace Lab3.Services;

public class SteganographyService : ISteganographyService
{
    private const string START_MARKER = "<<STEGO_START>>";
    private const string END_MARKER = "<<STEGO_END>>";

    public async Task<CryptoResult> EmbedTextAsync(string imagePath, string text, int bitsPerChannel)
    {
        try
        {
            if (bitsPerChannel < 1 || bitsPerChannel > 5)
                return CryptoResult.Error("Биты: 1-5");

            var fullText = START_MARKER + text + END_MARKER;
            var bytes = System.Text.Encoding.UTF8.GetBytes(fullText);

            var file = await StorageFile.GetFileFromPathAsync(imagePath);
            using var readStream = await file.OpenAsync(FileAccessMode.Read);

            var decoder = await BitmapDecoder.CreateAsync(readStream);
            var pixelData = await decoder.GetPixelDataAsync();
            var pixels = pixelData.DetachPixelData();

            int width = (int)decoder.PixelWidth;
            int height = (int)decoder.PixelHeight;
            int bitsPerPixel = bitsPerChannel * 3;
            int totalBits = bytes.Length * 8;

            if (totalBits > width * height * bitsPerPixel)
                return CryptoResult.Error($"Текст слишком большой! Макс. {(width * height * bitsPerPixel) / 8} байт");

            int bitIdx = 0;
            foreach (var byteVal in bytes)
            {
                for (int pos = 7; pos >= 0 && bitIdx < totalBits; pos--)
                {
                    int pIdx = bitIdx / bitsPerPixel;
                    int chIdx = (bitIdx % bitsPerPixel) / bitsPerChannel;
                    int bitPos = (bitIdx % bitsPerPixel) % bitsPerChannel;

                    if (pIdx >= width * height) break;

                    int offset = pIdx * 4;
                    byte orig = pixels[offset + chIdx];
                    int bit = (byteVal >> pos) & 1;

                    int mask = ~((1 << bitsPerChannel) - 1);
                    pixels[offset + chIdx] = (byte)((orig & mask) | (bit << (bitsPerChannel - 1 - bitPos)));

                    bitIdx++;
                }
            }

            using var outStream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outStream);
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore,
                (uint)width, (uint)height, 96, 96, pixels);
            await encoder.FlushAsync();

            // 🔥 Используем надёжный метод:
            var imageData = await outStream.ToByteArrayAsync();
            return CryptoResult.Ok(imageData);  // ✅ Используем новый фабричный метод
        }
        catch (Exception ex)
        {
            return CryptoResult.Error($"Ошибка внедрения: {ex.Message}");
        }
    }

    public async Task<CryptoResult> ExtractTextAsync(string imagePath, int bitsPerChannel, int expectedLength = -1)
    {
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(imagePath);
            using var stream = await file.OpenAsync(FileAccessMode.Read);
            var decoder = await BitmapDecoder.CreateAsync(stream);
            var pixels = (await decoder.GetPixelDataAsync()).DetachPixelData();

            int width = (int)decoder.PixelWidth;
            int height = (int)decoder.PixelHeight;
            int bitsPerPixel = bitsPerChannel * 3;

            var bytes = new List<byte>();
            int curByte = 0, bitsCollected = 0;

            for (int i = 0; i < width * height * bitsPerPixel; i++)
            {
                int pIdx = i / bitsPerPixel;
                int chIdx = (i % bitsPerPixel) / bitsPerChannel;
                int bitPos = (i % bitsPerPixel) % bitsPerChannel;

                if (pIdx >= pixels.Length / 4) break;

                byte val = pixels[pIdx * 4 + chIdx];
                int bit = (val >> (bitsPerChannel - 1 - bitPos)) & 1;

                curByte = (curByte << 1) | bit;
                bitsCollected++;

                if (bitsCollected == 8)
                {
                    bytes.Add((byte)curByte);
                    curByte = 0;
                    bitsCollected = 0;

                    var txt = System.Text.Encoding.UTF8.GetString(bytes.ToArray());
                    if (txt.Contains(END_MARKER))
                    {
                        int s = txt.IndexOf(START_MARKER);
                        int e = txt.IndexOf(END_MARKER);
                        if (s >= 0 && e > s)
                        {
                            var result = txt.Substring(s + START_MARKER.Length, e - s - START_MARKER.Length);
                            return CryptoResult.Ok(result);
                        }
                    }
                }
            }
            return CryptoResult.Error("Текст не найден или повреждён");
        }
        catch (Exception ex)
        {
            return CryptoResult.Error($"Ошибка извлечения: {ex.Message}");
        }
    }

    public async Task<bool> SaveImageAsync(IRandomAccessStream stream, string savePath)
    {
        try
        {
            using var fs = new FileStream(savePath, FileMode.Create, FileAccess.Write);
            using var input = stream.AsStreamForRead();
            await input.CopyToAsync(fs);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

// Extension method
// Lab3/Services/SteganographyService.cs (в конце файла)
public static class StreamExtensions
{
    public static byte[] ToArray(this IRandomAccessStream stream)
    {
        // 🔥 Сбрасываем позицию в начало
        stream.Seek(0);

        using var reader = new DataReader(stream.GetInputStreamAt(0));
        var loadTask = reader.LoadAsync((uint)stream.Size).AsTask();
        loadTask.Wait();  // Ждём загрузки

        var buffer = new byte[stream.Size];
        reader.ReadBytes(buffer);
        return buffer;
    }

    // 🔥 Альтернативный метод через .NET Stream (более надёжный)
    public static async Task<byte[]> ToByteArrayAsync(this IRandomAccessStream stream)
    {
        stream.Seek(0);
        using var netStream = stream.AsStreamForRead();
        using var ms = new MemoryStream();
        await netStream.CopyToAsync(ms);
        return ms.ToArray();
    }
}