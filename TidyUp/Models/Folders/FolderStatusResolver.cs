using MaterialDesignThemes.Wpf;
using TidyUp.Models.Enums;
using TidyUp.Services.Folders;
using TidyUp.Services.Monitoring;
using TidyUp.Styles;

namespace TidyUp.Models.Folders;

/// <summary>Collapses the physical check and the watcher state into one <see cref="FolderStatus"/>.</summary>
public static class FolderStatusResolver
{
    /// <summary>
    /// What is wrong with the path wins over what the watcher says: a folder that is missing is "Missing"
    /// whatever the watcher last reported.
    /// </summary>
    public static FolderStatus Resolve(
        FolderValidationStatus validation,
        WatcherHealthStatus watcher,
        bool isSystemProtected = false)
    {
        switch (validation)
        {
            case FolderValidationStatus.Missing:
                return FolderStatus.Missing;
            case FolderValidationStatus.AccessDenied:
                return FolderStatus.AccessDenied;
            case FolderValidationStatus.InvalidPath:
                return FolderStatus.Error;
        }

        if (isSystemProtected)
        {
            return FolderStatus.Protected;
        }

        return watcher switch
        {
            WatcherHealthStatus.Healthy => FolderStatus.Healthy,
            WatcherHealthStatus.Paused => FolderStatus.Paused,
            WatcherHealthStatus.Degraded => FolderStatus.Degraded,
            WatcherHealthStatus.Disconnected => FolderStatus.Missing,
            _ => FolderStatus.Error
        };
    }
}

/// <summary>How each <see cref="FolderStatus"/> is worded and coloured, so every surface looks the same.</summary>
public static class FolderStatusPresentation
{
    public static string Text(FolderStatus status) => status switch
    {
        FolderStatus.Healthy => "Healthy",
        FolderStatus.Paused => "Paused",
        FolderStatus.Degraded => "Degraded",
        FolderStatus.Missing => "Missing",
        FolderStatus.AccessDenied => "Access denied",
        FolderStatus.Protected => "Protected",
        _ => "Error"
    };

    public static string Description(FolderStatus status) => status switch
    {
        FolderStatus.Healthy => "Folder is accessible and actively watched.",
        FolderStatus.Paused => "Monitoring of this folder is paused.",
        FolderStatus.Degraded => "The watcher missed events and is reconciling; some files may be processed late.",
        FolderStatus.Missing => "Folder does not exist, or its drive or network share is disconnected.",
        FolderStatus.AccessDenied => "TidyUp cannot read or write this folder.",
        FolderStatus.Protected => "Protected system location. TidyUp will not move or delete files here.",
        _ => "The path is invalid or the watcher hit an unrecoverable error."
    };

    public static PackIconKind Icon(FolderStatus status) => status switch
    {
        FolderStatus.Healthy => PackIconKind.CheckCircleOutline,
        FolderStatus.Paused => PackIconKind.PauseCircleOutline,
        FolderStatus.Degraded => PackIconKind.AlertOutline,
        FolderStatus.Missing => PackIconKind.FolderRemoveOutline,
        FolderStatus.AccessDenied => PackIconKind.LockOutline,
        FolderStatus.Protected => PackIconKind.ShieldLockOutline,
        _ => PackIconKind.CloseCircleOutline
    };

    /// <summary>Token for the pill's tinted background.</summary>
    public static string BackgroundToken(FolderStatus status) => status switch
    {
        FolderStatus.Healthy => ThemeTokens.SuccessBackground,
        FolderStatus.Paused => ThemeTokens.NeutralBackground,
        FolderStatus.Degraded or FolderStatus.Protected => ThemeTokens.WarningBackground,
        _ => ThemeTokens.ErrorBackground
    };

    /// <summary>Token for the pill's text and icon; each is contrast-checked against its background token.</summary>
    public static string ForegroundToken(FolderStatus status) => status switch
    {
        FolderStatus.Healthy => ThemeTokens.Success,
        FolderStatus.Paused => ThemeTokens.Neutral,
        FolderStatus.Degraded or FolderStatus.Protected => ThemeTokens.Warning,
        _ => ThemeTokens.Error
    };
}
