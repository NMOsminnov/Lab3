using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Lab3.Helpers;

public static class FileHelper
{
    public static async Task<string?> PickFileAsync(IEnumerable<string> fileTypes, string title = "")
    {
        var picker = new FileOpenPicker();
        foreach (var ext in fileTypes) picker.FileTypeFilter.Add(ext);

        var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    public static async Task<string?> PickSaveFileAsync(string fileType, string suggestedName)
    {
        var picker = new FileSavePicker();
        picker.FileTypeChoices.Add(fileType, new[] { fileType });
        picker.SuggestedFileName = suggestedName;

        var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }
}