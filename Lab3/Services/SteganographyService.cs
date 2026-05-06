using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Lab3.Models;

namespace Lab3.Services;

public class SteganographyService : ISteganographyService
{
    private readonly ILogService _log;
    private const string START_MARKER = "<<STEGO_START>>";
    private const string END_MARKER = "<<STEGO_END>>";

    public SteganographyService(ILogService log) => _log = log;

    public async Task<CryptoResult> EmbedTextAsync(string imagePath, string text, StegoSettings settings)
    {
        _log.LogInfo($"[Embed] START: file={Path.GetFileName(imagePath)}, text_len={text.Length}, bits={settings.BitsPerChannel}, mode={settings.EmbeddingMode}");
        _log.LogInfo($"[Embed] Options: Hill={settings.UseHillCipher}, ZIP={settings.UseZipCompression}, Hamming={settings.UseHammingCode}");

        try
        {

            if (settings.UseZipCompression)
            {
                _log.LogWarning("[Embed] ZIP disabled for LSB steganography (format incompatible)");
                settings.UseZipCompression = false; // Принудительно отключаем
            }

            if (settings.BitsPerChannel < 1 || settings.BitsPerChannel > 5)
            {
                _log.LogError("[Embed] Invalid bits per channel");
                return CryptoResult.Error("Биты на канал: 1-5");
            }

            var file = await StorageFile.GetFileFromPathAsync(imagePath);
            _log.LogDebug($"[Embed] File loaded: {file.Name}, type={file.FileType}");

            using var readStream = await file.OpenAsync(FileAccessMode.Read);
            var decoder = await BitmapDecoder.CreateAsync(readStream);
            var pixelData = await decoder.GetPixelDataAsync();
            var pixels = pixelData.DetachPixelData();

            int width = (int)decoder.PixelWidth;
            int height = (int)decoder.PixelHeight;
            _log.LogDebug($"[Embed] Image decoded: {width}x{height}, format={decoder.BitmapPixelFormat}");

            byte[] dataBytes = System.Text.Encoding.UTF8.GetBytes(text);
            _log.LogDebug($"[Embed] Text → bytes: {text.Length} chars → {dataBytes.Length} bytes");
            LogBytesPreview("[Embed] Original bytes", dataBytes, 20);

            //  Hill cipher
            if (settings.UseHillCipher)
            {
                _log.LogInfo("[Embed] Applying Hill cipher");
                var hillService = new HillCipherService(_log);
                var hillSettings = new HillCipherSettings
                {
                    Modulo = 256,
                    KeyMatrix = settings.HillKeyMatrix ?? hillService.GenerateRandomKey(settings.HillKeySize, 256)
                };

                if (settings.HillKeyMatrix != null)
                    LogMatrix("[Embed] Using provided key", settings.HillKeyMatrix);
                else
                    _log.LogDebug($"[Embed] Generated random {settings.HillKeySize}x{settings.HillKeySize} key");

                dataBytes = hillService.Encrypt(dataBytes, hillSettings);
                _log.LogDebug($"[Embed] After Hill: {dataBytes.Length} bytes");
                LogBytesPreview("[Embed] After Hill", dataBytes, 20);
            }

            //  ZIP compression
            if (settings.UseZipCompression)
            {
                _log.LogInfo("[Embed] Compressing with ZIP");
                int before = dataBytes.Length;
                dataBytes = CompressionService.Compress(dataBytes);
                _log.LogDebug($"[Embed] After ZIP: {before} → {dataBytes.Length} bytes");
                LogBytesPreview("[Embed] After ZIP", dataBytes, 20);
            }

            //  Hamming encoding
            if (settings.UseHammingCode)
            {
                _log.LogInfo("[Embed] Encoding with Hamming (7,4)");
                int before = dataBytes.Length;
                dataBytes = HammingService.Encode(dataBytes);
                _log.LogDebug($"[Embed] After Hamming: {before} → {dataBytes.Length} bytes");
            }

            //  Markers
            var startMarker = System.Text.Encoding.UTF8.GetBytes(START_MARKER);
            var endMarker = System.Text.Encoding.UTF8.GetBytes(END_MARKER);
            var lengthBytes = BitConverter.GetBytes(dataBytes.Length); // 4 байта длины

            var fullData = startMarker
                .Concat(lengthBytes)
                .Concat(dataBytes)
                .Concat(endMarker)
                .ToArray();

            _log.LogDebug($"[Embed] Structure: START(15) + LEN(4) + DATA({dataBytes.Length}) + END(13) = {fullData.Length}B");

            LogBytesPreview("[Embed] With markers (start)", fullData.Take(30).ToArray(), 30);

            //  Capacity check
            int bitsPerPixel = settings.BitsPerChannel * 3;
            int totalBitsAvailable = width * height * bitsPerPixel;
            int totalBitsNeeded = fullData.Length * 8;
            _log.LogDebug($"[Embed] Capacity: {totalBitsAvailable} bits available, {totalBitsNeeded} bits needed");

            if (totalBitsNeeded > totalBitsAvailable)
            {
                _log.LogError($"[Embed] Data too large: need {totalBitsNeeded} bits, have {totalBitsAvailable}");
                return CryptoResult.Error($"Текст слишком большой! Макс. {totalBitsAvailable / 8} байт");
            }

            //  Embed bits
            _log.LogDebug($"[Embed] Embedding {totalBitsNeeded} bits into {width * height} pixels");
            EmbedBits(pixels, fullData, width, height, settings);
            _log.LogDebug("[Embed] Bit embedding complete");

            //  Encode as BMP
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
            _log.LogDebug("[Embed] BMP encoding complete");

            var imageData = await StreamToBytesAsync(outStream);
            _log.LogInfo($"[Embed] SUCCESS: output={imageData.Length} bytes");

            return CryptoResult.Ok(imageData);
        }
        catch (Exception ex)
        {
            _log.LogError($"[Embed] FAILED: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            return CryptoResult.Error($"Ошибка внедрения: {ex.Message}");
        }
    }

    public async Task<CryptoResult> ExtractTextAsync(string imagePath, StegoSettings settings)
    {
        _log.LogInfo($"[Extract] START: file={Path.GetFileName(imagePath)}, bits={settings.BitsPerChannel}, mode={settings.EmbeddingMode}");
        _log.LogInfo($"[Extract] Options: Hill={settings.UseHillCipher}, ZIP={settings.UseZipCompression}, Hamming={settings.UseHammingCode}");

        try
        {
            if (settings.UseZipCompression)
            {
                _log.LogWarning("[Extract] ZIP disabled for LSB steganography (format incompatible)");
                settings.UseZipCompression = false;
            }

            var file = await StorageFile.GetFileFromPathAsync(imagePath);
            using var stream = await file.OpenAsync(FileAccessMode.Read);
            var decoder = await BitmapDecoder.CreateAsync(stream);
            var pixelData = await decoder.GetPixelDataAsync();
            var pixels = pixelData.DetachPixelData();

            int width = (int)decoder.PixelWidth;
            int height = (int)decoder.PixelHeight;
            int bitsPerPixel = settings.BitsPerChannel * 3;

            _log.LogDebug($"[Extract] Image: {width}x{height}, bitsPerPixel={bitsPerPixel}");

            var extractedBytes = ExtractBits(pixels, width, height, bitsPerPixel, settings);
            _log.LogDebug($"[Extract] Extracted {extractedBytes.Length} bytes from LSB");

            if (extractedBytes.Length == 0)
            {
                _log.LogError("[Extract] No data extracted");
                return CryptoResult.Error("Не удалось извлечь данные");
            }

            // 🔹 1. Ищем START-маркер
            var startMarker = System.Text.Encoding.UTF8.GetBytes(START_MARKER);
            int startIdx = FindBytes(extractedBytes, startMarker);

            if (startIdx < 0)
            {
                _log.LogError("[Extract] START marker not found");
                return CryptoResult.Error("Маркер начала не найден");
            }
            _log.LogDebug($"[Extract] START marker found at index {startIdx}");

            // 🔹 2. Читаем длину данных (4 байта сразу после маркера)
            int lengthIdx = startIdx + startMarker.Length;
            if (lengthIdx + 4 > extractedBytes.Length)
                return CryptoResult.Error("Недостаточно данных для чтения длины");

            int dataLength = BitConverter.ToInt32(extractedBytes, lengthIdx);
            _log.LogDebug($"[Extract] Declared data length: {dataLength} bytes");

            if (dataLength <= 0 || dataLength > extractedBytes.Length / 2)
                return CryptoResult.Error($"Некорректная длина данных: {dataLength}");

            // 🔹 3. Извлекаем ровно dataLength байт полезной нагрузки
            int payloadStart = lengthIdx + 4;
            int payloadEnd = payloadStart + dataLength;

            if (payloadEnd > extractedBytes.Length)
                return CryptoResult.Error($"Данные обрезаны: нужно {payloadEnd}, есть {extractedBytes.Length}");

            var dataBytes = new byte[dataLength];
            Array.Copy(extractedBytes, payloadStart, dataBytes, 0, dataLength);
            LogBytesPreview("[Extract] Payload", dataBytes, 20);

            // 🔹 4. Декодирование Хэмминга
            if (settings.UseHammingCode)
            {
                _log.LogInfo("[Extract] Decoding Hamming");
                int before = dataBytes.Length;
                dataBytes = HammingService.Decode(dataBytes);
                _log.LogDebug($"[Extract] After Hamming: {before} → {dataBytes.Length} bytes");
            }

            // 🔹 5. Расшифровка Хилла
            if (settings.UseHillCipher && settings.HillKeyMatrix != null)
            {
                _log.LogInfo("[Extract] Decrypting Hill cipher");
                LogMatrix("[Extract] Using key", settings.HillKeyMatrix);

                var hillService = new HillCipherService(_log);
                var hillSettings = new HillCipherSettings
                {
                    Modulo = 256,
                    KeyMatrix = settings.HillKeyMatrix
                };
                dataBytes = hillService.Decrypt(dataBytes, hillSettings);
                _log.LogDebug($"[Extract] After Hill decrypt: {dataBytes.Length} bytes");
                LogBytesPreview("[Extract] After Hill", dataBytes, 20);
            }

            string resultText = System.Text.Encoding.UTF8.GetString(dataBytes);
            _log.LogInfo($"[Extract] SUCCESS: decoded {resultText.Length} chars: '{resultText}'");

            return CryptoResult.Ok(resultText);
        }
        catch (Exception ex)
        {
            _log.LogError($"[Extract] FAILED: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            return CryptoResult.Error($"Ошибка извлечения: {ex.Message}");
        }
    }

    public async Task<bool> SaveImageAsync(IRandomAccessStream stream, string savePath)
    {
        try
        {
            if (!savePath.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
                savePath = Path.ChangeExtension(savePath, ".bmp");

            using var fs = new FileStream(savePath, FileMode.Create, FileAccess.Write);
            using var input = stream.AsStreamForRead();
            await input.CopyToAsync(fs);

            _log.LogDebug($"[Save] Image saved: {savePath}, {fs.Length} bytes");
            return true;
        }
        catch (Exception ex)
        {
            _log.LogError($"[Save] Failed: {ex.Message}");
            return false;
        }
    }

    //  Helper methods

    private void EmbedBits(byte[] pixels, byte[] data, int width, int height, StegoSettings settings)
    {
        int bitsPerPixel = settings.BitsPerChannel * 3;
        int bitIdx = 0;
        int totalBits = data.Length * 8;

        foreach (byte byteVal in data)
        {
            for (int pos = 7; pos >= 0 && bitIdx < totalBits; pos--)
            {
                int pixelIndex = GetPixelIndex(bitIdx / bitsPerPixel, width, height, settings.EmbeddingMode);
                if (pixelIndex >= width * height) break;

                int channelIndex = (bitIdx % bitsPerPixel) / settings.BitsPerChannel;
                int bitPosition = (bitIdx % bitsPerPixel) % settings.BitsPerChannel;

                int offset = pixelIndex * 4;
                byte originalValue = pixels[offset + channelIndex];
                int bitToEmbed = (byteVal >> pos) & 1;

                int mask = ~((1 << settings.BitsPerChannel) - 1);
                pixels[offset + channelIndex] = (byte)(
                    (originalValue & mask) |
                    (bitToEmbed << (settings.BitsPerChannel - 1 - bitPosition))
                );

                bitIdx++;
            }
        }
        _log.LogDebug($"[EmbedBits] Embedded {bitIdx} bits into {width * height} pixels");
    }

    private byte[] ExtractBits(byte[] pixels, int width, int height, int bitsPerPixel, StegoSettings settings)
    {
        var result = new List<byte>();
        int curByte = 0;
        int bitsCollected = 0;

        for (int i = 0; i < width * height * bitsPerPixel; i++)
        {
            int pixelIndex = GetPixelIndex(i / bitsPerPixel, width, height, settings.EmbeddingMode);
            if (pixelIndex >= width * height) break;

            int channelIndex = (i % bitsPerPixel) / settings.BitsPerChannel;
            int bitPosition = (i % bitsPerPixel) % settings.BitsPerChannel;

            int offset = pixelIndex * 4;
            byte value = pixels[offset + channelIndex];
            int bit = (value >> (settings.BitsPerChannel - 1 - bitPosition)) & 1;

            curByte = (curByte << 1) | bit;
            bitsCollected++;

            if (bitsCollected == 8)
            {
                result.Add((byte)curByte);
                curByte = 0;
                bitsCollected = 0;
            }
        }
        _log.LogDebug($"[ExtractBits] Extracted {result.Count} bytes from {bitsCollected / 8 * 8} bits");
        return result.ToArray();
    }

    private int GetPixelIndex(int linearIndex, int width, int height, EmbeddingMode mode)
    {
        return mode switch
        {
            EmbeddingMode.RowMajor => linearIndex,
            EmbeddingMode.ColumnMajor => (linearIndex % width) * height + (linearIndex / width),
            _ => linearIndex
        };
    }

    private int FindBytes(byte[] haystack, byte[] needle, int startIndex = 0)
    {
        if (needle.Length == 0 || haystack.Length - startIndex < needle.Length)
            return -1;

        for (int i = startIndex; i <= haystack.Length - needle.Length; i++)
        {
            bool found = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    found = false;
                    break;
                }
            }
            if (found) return i;
        }
        return -1;
    }

    private async Task<byte[]> StreamToBytesAsync(IRandomAccessStream stream)
    {
        stream.Seek(0);
        using var reader = new DataReader(stream.GetInputStreamAt(0));
        await reader.LoadAsync((uint)stream.Size);
        var buffer = new byte[stream.Size];
        reader.ReadBytes(buffer);
        return buffer;
    }

    //  Debug helpers

    private void LogBytesPreview(string prefix, byte[] bytes, int maxLen)
    {
        if (_log == null) return;
        int len = Math.Min(bytes.Length, maxLen);
        var preview = BitConverter.ToString(bytes.Take(len).ToArray()).Replace("-", " ");
        _log.LogDebug($"{prefix}: [{len}/{bytes.Length}] {preview}{(bytes.Length > maxLen ? "..." : "")}");
    }

    private void LogMatrix(string prefix, int[,] matrix)
    {
        if (_log == null) return;
        int n = matrix.GetLength(0);
        var sb = new System.Text.StringBuilder($"{prefix}: {n}x{n}\n[");
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
                sb.Append(matrix[i, j].ToString().PadLeft(4));
            if (i < n - 1) sb.AppendLine();
        }
        sb.Append("]");
        _log.LogDebug(sb.ToString());
    }
}