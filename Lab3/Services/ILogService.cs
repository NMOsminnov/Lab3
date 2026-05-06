using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Lab3.Services;

public interface ILogService
{
    ObservableCollection<string> Logs { get; }

    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message);
    void LogDebug(string message);

    Task LoadLogsAsync();
    Task SaveLogsAsync();
    void ClearLogs();
}