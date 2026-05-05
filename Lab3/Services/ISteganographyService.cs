using Lab3.Models;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace Lab3.Services;

public interface ISteganographyService
{
    Task<CryptoResult> EmbedTextAsync(string imagePath, string text, int bitsPerChannel);
    Task<CryptoResult> ExtractTextAsync(string imagePath, int bitsPerChannel, int expectedLength = -1);
    Task<bool> SaveImageAsync(IRandomAccessStream imageStream, string savePath);
}