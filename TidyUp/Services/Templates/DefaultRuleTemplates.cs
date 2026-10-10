using System.Collections.ObjectModel;
using System.IO;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;

namespace TidyUp.Services.Templates;

/// <summary>
/// Library of pre-packaged starter rule templates for common organization workflows.
/// </summary>
public static class DefaultRuleTemplates
{
    public const string TemplateOrganizeDownloads = "Organize Downloads by File Type (PDFs, Images, Archives, Installers)";
    public const string TemplateArchiveScreenshots = "Archive Screenshots older than 30 days";
    public const string TemplateSortPhotos = "Sort Camera Photos by Year/Month taken";

    /// <summary>
    /// Returns all available pre-packaged rule templates.
    /// </summary>
    public static IReadOnlyList<Rule> GetAllTemplates()
    {
        return
        [
            CreateOrganizeDownloadsTemplate(),
            CreateArchiveScreenshotsTemplate(),
            CreateSortPhotosTemplate()
        ];
    }

    /// <summary>
    /// Creates a new rule instance instantiated from the specified template name.
    /// </summary>
    public static Rule CreateFromTemplate(string templateName)
    {
        return templateName switch
        {
            TemplateOrganizeDownloads => CreateOrganizeDownloadsTemplate(),
            TemplateArchiveScreenshots => CreateArchiveScreenshotsTemplate(),
            TemplateSortPhotos => CreateSortPhotosTemplate(),
            _ => throw new ArgumentException($"Unknown rule template: '{templateName}'", nameof(templateName))
        };
    }

    /// <summary>
    /// Template 1: Sorts downloads by extension group.
    /// </summary>
    public static Rule CreateOrganizeDownloadsTemplate()
    {
        var downloadsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");

        var rule = new Rule
        {
            Id = Guid.NewGuid(),
            Name = "Organize Downloads by File Type",
            Description = "Automatically sorts incoming downloads into PDFs, Images, Archives, and Installers subfolders.",
            IsEnabled = false, // Templates start in Disabled state by default
            ExecutionOrder = 1,
            StopProcessingAfterMatch = true,
            MonitoredFolders =
            [
                new MonitoredFolder
                {
                    Path = downloadsPath,
                    IncludeSubfolders = false
                }
            ],
            Conditions = new ConditionGroup
            {
                Operator = LogicOperator.Or,
                Conditions =
                [
                    new FileExtensionCondition { Value = "pdf" },
                    new FileExtensionCondition { Value = "jpg" },
                    new FileExtensionCondition { Value = "png" },
                    new FileExtensionCondition { Value = "zip" },
                    new FileExtensionCondition { Value = "exe" }
                ]
            },
            Actions =
            [
                new MoveFileAction
                {
                    DestinationPath = Path.Combine(downloadsPath, "Organized"),
                    ConflictResolution = ConflictResolution.RenameNew,
                    PreserveSubfolderStructure = false
                }
            ]
        };

        return rule;
    }

    /// <summary>
    /// Template 2: Archives screenshots older than 30 days.
    /// </summary>
    public static Rule CreateArchiveScreenshotsTemplate()
    {
        var picturesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "Screenshots");

        var rule = new Rule
        {
            Id = Guid.NewGuid(),
            Name = "Archive Screenshots older than 30 days",
            Description = "Moves old screenshot files to an Archive folder to keep the active screenshot folder clean.",
            IsEnabled = false,
            ExecutionOrder = 2,
            StopProcessingAfterMatch = true,
            MonitoredFolders =
            [
                new MonitoredFolder
                {
                    Path = picturesPath,
                    IncludeSubfolders = false
                }
            ],
            Conditions = new ConditionGroup
            {
                Operator = LogicOperator.And,
                Conditions =
                [
                    new FileNameCondition
                    {
                        Operator = StringOperator.Contains,
                        Value = "Screenshot"
                    },
                    new FileDateCondition
                    {
                        Operator = FileDateCondition.DateOperator.OlderThanDays,
                        DaysOld = 30
                    }
                ]
            },
            Actions =
            [
                new MoveFileAction
                {
                    DestinationPath = Path.Combine(picturesPath, "Archive"),
                    ConflictResolution = ConflictResolution.RenameNew
                }
            ]
        };

        return rule;
    }

    /// <summary>
    /// Template 3: Sorts camera photos into Year/Month subfolders.
    /// </summary>
    public static Rule CreateSortPhotosTemplate()
    {
        var photosPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "Camera");

        var rule = new Rule
        {
            Id = Guid.NewGuid(),
            Name = "Sort Camera Photos by Year/Month taken",
            Description = "Organizes incoming camera photos into Year and Month subfolders.",
            IsEnabled = false,
            ExecutionOrder = 3,
            StopProcessingAfterMatch = true,
            MonitoredFolders =
            [
                new MonitoredFolder
                {
                    Path = photosPath,
                    IncludeSubfolders = false
                }
            ],
            Conditions = new ConditionGroup
            {
                Operator = LogicOperator.Or,
                Conditions =
                [
                    new FileExtensionCondition { Value = "jpg" },
                    new FileExtensionCondition { Value = "jpeg" },
                    new FileExtensionCondition { Value = "png" },
                    new FileExtensionCondition { Value = "cr2" },
                    new FileExtensionCondition { Value = "nef" }
                ]
            },
            Actions =
            [
                new MoveFileAction
                {
                    DestinationPath = Path.Combine(photosPath, "{date_year}", "{date_month}"),
                    ConflictResolution = ConflictResolution.RenameNew
                }
            ]
        };

        return rule;
    }
}

