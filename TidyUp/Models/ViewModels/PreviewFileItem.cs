using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TidyUp.Models.ViewModels;

/// <summary>
/// Represents a file in the condition editor's live preview.
/// </summary>
public partial class PreviewFileItem : ObservableObject
{
    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _folderPath = string.Empty;

    [ObservableProperty]
    private bool _matches;

    [ObservableProperty]
    private long _fileSize;

    [ObservableProperty]
    private DateTime _modifiedDate;

    public string FullPath => Path.Combine(FolderPath, FileName);

    public string FileSizeDisplay
    {
        get
        {
            if (FileSize < 1024)
                return $"{FileSize} B";
            if (FileSize < 1024 * 1024)
                return $"{FileSize / 1024.0:F1} KB";
            if (FileSize < 1024 * 1024 * 1024)
                return $"{FileSize / (1024.0 * 1024.0):F1} MB";
            return $"{FileSize / (1024.0 * 1024.0 * 1024.0):F1} GB";
        }
    }
}
