using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Lab3.Services;

public class ImageDistortionService
{
    public async Task<byte[]> DistortImageAsync(byte[] imageBytes, int errorCount)
    {
        if (imageBytes == null || imageBytes.Length == 0)
            throw new ArgumentException("Изображение пусто");

        // Загружаем BMP из байтов
        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(imageBytes.AsBuffer());
        stream.Seek(0);

        var decoder = await BitmapDecoder.CreateAsync(stream);
        var pixelData = await decoder.GetPixelDataAsync();
        var pixels = pixelData.DetachPixelData();

        int width = (int)decoder.PixelWidth;
        int height = (int)decoder.PixelHeight;
        int totalPixels = width * height;

        // Ограничиваем количество ошибок, чтобы не выйти за границы массива
        // Максимум ошибок = кол-во пикселей * 3 канала
        int maxErrors = totalPixels * 3;
        int actualErrors = Math.Min(errorCount, maxErrors);

        var random = new Random();
        for (int i = 0; i < actualErrors; i++)
        {
            // Выбираем случайный пиксель и случайный канал (0=B, 1=G, 2=R)
            int pIdx = random.Next(totalPixels);
            int chIdx = random.Next(3);

            int offset = pIdx * 4 + chIdx;

            // Инвертируем младший бит (LSB)
            pixels[offset] ^= 1;
        }

        // Сохраняем искаженное изображение обратно в байты
        using var outStream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.BmpEncoderId, outStream);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Ignore,
            (uint)width,
            (uint)height,
            96, 96,
            pixels);
        await encoder.FlushAsync();

        return await outStream.ToByteArrayAsync();
    }
}