using System.Threading.Tasks;
using Windows.Storage.Streams;
using Lab3.Models;

namespace Lab3.Services;

public interface ISteganographyService
{
    // 🔥 Новая сигнатура с настройками
    Task<CryptoResult> EmbedTextAsync(string imagePath, string text, StegoSettings settings);

    // 🔥 Новая сигнатура с настройками
    Task<CryptoResult> ExtractTextAsync(string imagePath, StegoSettings settings);

    Task<bool> SaveImageAsync(IRandomAccessStream imageStream, string savePath);
}