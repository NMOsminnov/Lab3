using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;

namespace Lab3.Services;

public class LogService : ILogService
{
    private const string LOG_FILENAME = "app_log.txt";
    private const int MAX_LOG_LINES = 500;
    private const int MAX_FILE_LINES = 2000;

    public ObservableCollection<string> Logs { get; } = new();

    private readonly string _logFolderPath;
    private bool _isLoading;
    private readonly object _fileLock = new();

    public LogService()
    {
        _logFolderPath = ApplicationData.Current.LocalFolder.Path;
    }

    public void LogInfo(string message) => WriteLog("INFO", message);
    public void LogWarning(string message) => WriteLog("WARN", message);
    public void LogError(string message) => WriteLog("ERROR", message);
    public void LogDebug(string message) => WriteLog("DEBUG", message);

    private void WriteLog(string level, string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var logEntry = $"[{timestamp}] [{level}] {message}";

        Logs.Add(logEntry);
        if (Logs.Count > MAX_LOG_LINES) Logs.RemoveAt(0);

        _ = WriteToFileAsync(logEntry);
    }

    private async Task WriteToFileAsync(string logEntry)
    {
        try
        {
            var filePath = Path.Combine(_logFolderPath, LOG_FILENAME);

            lock (_fileLock)
            {
                if (File.Exists(filePath))
                {
                    var info = new FileInfo(filePath);
                    if (info.Length > 10 * 1024 * 1024)
                        RotateLogs(filePath);
                }
            }

            await Task.Run(() =>
            {
                lock (_fileLock)
                {
                    File.AppendAllText(filePath, logEntry + Environment.NewLine);
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LogService] Write error: {ex.Message}");
        }
    }

    private void RotateLogs(string filePath)
    {
        try
        {
            var archivePath = Path.Combine(_logFolderPath, $"app_log_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            File.Move(filePath, archivePath);

            var dir = new DirectoryInfo(_logFolderPath);
            foreach (var f in dir.GetFiles("app_log_*.txt"))
            {
                if (DateTime.Now - f.CreationTime > TimeSpan.FromDays(7))
                    f.Delete();
            }
        }
        catch { }
    }

    public async Task LoadLogsAsync()
    {
        if (_isLoading) return;
        _isLoading = true;

        try
        {
            Logs.Clear();
            var filePath = Path.Combine(_logFolderPath, LOG_FILENAME);
            if (!File.Exists(filePath)) return;

            var recent = await ReadLastLinesAsync(filePath, MAX_LOG_LINES);
            foreach (var line in recent) Logs.Add(line);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LogService] Load error: {ex.Message}");
        }
        finally { _isLoading = false; }
    }

    private async Task<string[]> ReadLastLinesAsync(string filePath, int count)
    {
        return await Task.Run(() =>
        {
            var lines = new string[count];
            int idx = count - 1;

            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);

            long pos = stream.Length;
            var buffer = new char[1024];

            while (pos > 0 && idx >= 0)
            {
                int readSize = (int)Math.Min(buffer.Length, pos);
                pos -= readSize;
                stream.Seek(pos, SeekOrigin.Begin);

                reader.ReadBlock(buffer, 0, readSize);
                var text = new string(buffer, 0, readSize);
                var fileLines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                for (int i = fileLines.Length - 1; i >= 0 && idx >= 0; i--)
                    lines[idx--] = fileLines[i];
            }

            return lines.Where(l => l != null).ToArray();
        });
    }

    public async Task SaveLogsAsync()
    {
        var filePath = Path.Combine(_logFolderPath, LOG_FILENAME);
        try
        {
            lock (_fileLock)
            {
                File.WriteAllLines(filePath, Logs);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LogService] Save error: {ex.Message}");
        }
    }

    public void ClearLogs()
    {
        Logs.Clear();
        var filePath = Path.Combine(_logFolderPath, LOG_FILENAME);
        if (File.Exists(filePath)) File.Delete(filePath);
    }
}