using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Services.Rules;
using TidyUp.Services.Templates;
using Xunit;

namespace TidyUp.Tests.RuleTests;

public class SerializationTests
{
    private readonly RuleSerializationService _serializationService = new();

    [Fact]
    public void DefaultRuleTemplates_ProvidesThreePrePackagedTemplatesInDisabledState()
    {
        // Act
        var templates = DefaultRuleTemplates.GetAllTemplates();

        // Assert
        Assert.Equal(3, templates.Count);

        foreach (var template in templates)
        {
            Assert.False(template.IsEnabled, $"Template '{template.Name}' should be Disabled by default.");
            Assert.NotEmpty(template.MonitoredFolders);
            Assert.NotEmpty(template.Actions);
            Assert.NotNull(template.Conditions);
        }

        Assert.Contains(templates, t => t.Name.Contains("Downloads"));
        Assert.Contains(templates, t => t.Name.Contains("Screenshots"));
        Assert.Contains(templates, t => t.Name.Contains("Camera Photos"));
    }

    [Fact]
    public void DefaultRuleTemplates_CreateFromTemplate_GeneratesNewRuleInstance()
    {
        // Act
        var rule1 = DefaultRuleTemplates.CreateFromTemplate(DefaultRuleTemplates.TemplateOrganizeDownloads);
        var rule2 = DefaultRuleTemplates.CreateFromTemplate(DefaultRuleTemplates.TemplateOrganizeDownloads);

        // Assert
        Assert.NotEqual(rule1.Id, rule2.Id);
        Assert.False(rule1.IsEnabled);
        Assert.Equal("Organize Downloads by File Type", rule1.Name);
    }

    [Fact]
    public void ExportRules_IncludesSchemaAndVersionHeader()
    {
        // Arrange
        var rule = DefaultRuleTemplates.CreateOrganizeDownloadsTemplate();

        // Act
        string json = _serializationService.ExportRules([rule]);

        // Assert
        Assert.Contains("\"$schema\": \"https://tidyup.app/schemas/rules-v1.json\"", json);
        Assert.Contains("\"version\": \"1.0\"", json);
        Assert.Contains("\"application\": \"TidyUp\"", json);
        Assert.Contains("Organize Downloads by File Type", json);
    }

    [Fact]
    public void ImportRules_PreservesConditionsAndActionsAndForcesDisabledState()
    {
        // Arrange
        var originalRule = new Rule
        {
            Id = Guid.NewGuid(),
            Name = "Active Source Rule",
            IsEnabled = true, // Source is enabled
            MonitoredFolders = [new MonitoredFolder { Path = @"C:\TestFolder" }],
            Conditions = new ConditionGroup
            {
                Operator = LogicOperator.And,
                Conditions = [new FileExtensionCondition { Value = "pdf" }]
            },
            Actions = [new MoveFileAction { DestinationPath = @"C:\DestFolder" }]
        };

        string json = _serializationService.ExportRules([originalRule]);

        // Act
        var importResult = _serializationService.ImportRules(json);

        // Assert
        Assert.True(importResult.Success);
        Assert.Single(importResult.ImportedRules);

        var imported = importResult.ImportedRules[0];
        Assert.False(imported.IsEnabled, "Imported rule MUST be in Disabled state.");
        Assert.NotEqual(originalRule.Id, imported.Id);
        Assert.Equal("Active Source Rule", imported.Name);

        Assert.Single(imported.MonitoredFolders);
        Assert.Equal(@"C:\TestFolder", imported.MonitoredFolders[0].Path);

        Assert.NotNull(imported.Conditions);
        Assert.Single(imported.Conditions.Conditions);
        var extCond = Assert.IsType<FileExtensionCondition>(imported.Conditions.Conditions[0]);
        Assert.Equal("pdf", extCond.Value);

        Assert.Single(imported.Actions);
        var moveAction = Assert.IsType<MoveFileAction>(imported.Actions[0]);
        Assert.Equal(@"C:\DestFolder", moveAction.DestinationPath);
    }

    [Fact]
    public void ValidatePackageSchema_RejectsUnsupportedVersion()
    {
        // Arrange: JSON with unsupported version 2.0
        string invalidVersionJson = """
        {
          "$schema": "https://tidyup.app/schemas/rules-v2.json",
          "version": "2.0",
          "rules": [
            { "name": "Rule" }
          ]
        }
        """;

        // Act
        var result = _serializationService.ValidatePackageSchema(invalidVersionJson);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Unsupported schema version '2.0'", result.ErrorMessage);
    }

    [Fact]
    public void ImportRules_FlagsUnsafeOrReservedDevicePaths()
    {
        // Arrange: Rule targeting Windows reserved name NUL
        var unsafeRule = new Rule
        {
            Name = "Unsafe Rule",
            IsEnabled = true,
            MonitoredFolders = [new MonitoredFolder { Path = @"C:\Monitored" }],
            Actions = [new MoveFileAction { DestinationPath = @"C:\NUL\folder" }]
        };

        string json = _serializationService.ExportRules([unsafeRule]);

        // Act
        var result = _serializationService.ImportRules(json);

        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(result.Warnings);
        Assert.Contains(result.Warnings, w => w.Contains("reserved Windows device name"));
    }

    [Fact]
    public void ValidatePackageSchema_SupportsLegacyPackageWithoutSchemaUri()
    {
        // Arrange: Legacy 1.0 JSON export format without $schema or application headers
        string legacyJson = """
        {
          "version": "1.0",
          "exportedAt": "2025-01-01T00:00:00Z",
          "rules": [
            {
              "name": "Legacy Document Organizer",
              "monitoredFolders": [
                { "path": "C:\\LegacyDocs" }
              ],
              "actions": [
                {
                  "$type": "move",
                  "destinationPath": "C:\\ArchivedDocs"
                }
              ]
            }
          ]
        }
        """;

        // Act
        var result = _serializationService.ValidatePackageSchema(legacyJson);
        var importResult = _serializationService.ImportRules(legacyJson);

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
        Assert.True(importResult.Success);
        Assert.Single(importResult.ImportedRules);
        var imported = importResult.ImportedRules[0];
        Assert.Equal("Legacy Document Organizer", imported.Name);
        Assert.False(imported.IsEnabled, "Legacy rules must import as disabled.");
    }

    [Fact]
    public void ValidatePackageSchema_RejectsFutureVersionsWithDescriptiveMessage()
    {
        string futureJson = """
        {
          "version": "9.9",
          "rules": [
            {
              "name": "Future Rule",
              "monitoredFolders": [{ "path": "C:\\Test" }],
              "actions": [{ "$type": "move", "destinationPath": "C:\\Dest" }]
            }
          ]
        }
        """;

        var result = _serializationService.ValidatePackageSchema(futureJson);

        Assert.False(result.IsValid);
        Assert.Contains("Unsupported schema version '9.9'", result.ErrorMessage);
    }

    [Fact]
    public void ValidatePackageSchema_RejectsMissingVersionProperty()
    {
        string missingVersionJson = """
        {
          "rules": [
            {
              "name": "Rule",
              "monitoredFolders": [{ "path": "C:\\Test" }],
              "actions": [{ "$type": "move", "destinationPath": "C:\\Dest" }]
            }
          ]
        }
        """;

        var result = _serializationService.ValidatePackageSchema(missingVersionJson);

        Assert.False(result.IsValid);
        Assert.Contains("missing mandatory 'version' property", result.ErrorMessage);
    }

    [Fact]
    public void ValidatePackageSchema_RejectsMissingNameOrEmptyFoldersOrActions()
    {
        string missingNameJson = """
        {
          "version": "1.0",
          "rules": [
            {
              "name": "",
              "monitoredFolders": [{ "path": "C:\\Test" }],
              "actions": [{ "$type": "move", "destinationPath": "C:\\Dest" }]
            }
          ]
        }
        """;
        var r1 = _serializationService.ValidatePackageSchema(missingNameJson);
        Assert.False(r1.IsValid);
        Assert.Contains("missing a name", r1.ErrorMessage);

        string emptyFoldersJson = """
        {
          "version": "1.0",
          "rules": [
            {
              "name": "No Folders",
              "monitoredFolders": [],
              "actions": [{ "$type": "move", "destinationPath": "C:\\Dest" }]
            }
          ]
        }
        """;
        var r2 = _serializationService.ValidatePackageSchema(emptyFoldersJson);
        Assert.False(r2.IsValid);
        Assert.Contains("has no monitored folders", r2.ErrorMessage);

        string emptyActionsJson = """
        {
          "version": "1.0",
          "rules": [
            {
              "name": "No Actions",
              "monitoredFolders": [{ "path": "C:\\Test" }],
              "actions": []
            }
          ]
        }
        """;
        var r3 = _serializationService.ValidatePackageSchema(emptyActionsJson);
        Assert.False(r3.IsValid);
        Assert.Contains("has no actions", r3.ErrorMessage);
    }

    [Fact]
    public void ValidatePackageSchema_RejectsMalformedActionParameters()
    {
        string emptyDestJson = """
        {
          "version": "1.0",
          "rules": [
            {
              "name": "Bad Move Action",
              "monitoredFolders": [{ "path": "C:\\Test" }],
              "actions": [
                {
                  "$type": "move",
                  "destinationPath": ""
                }
              ]
            }
          ]
        }
        """;

        var result = _serializationService.ValidatePackageSchema(emptyDestJson);

        Assert.False(result.IsValid);
        Assert.Contains("missing or empty destination path", result.ErrorMessage);
    }

    [Fact]
    public void ImportRules_DisambiguatesDuplicateNamesAndReassignsUniqueGuids()
    {
        var fixedId = Guid.NewGuid();
        string duplicateBatchJson = $$"""
        {
          "version": "1.0",
          "rules": [
            {
              "id": "{{fixedId}}",
              "name": "Duplicate Name",
              "monitoredFolders": [{ "path": "C:\\Test1" }],
              "actions": [{ "$type": "move", "destinationPath": "C:\\Dest1" }]
            },
            {
              "id": "{{fixedId}}",
              "name": "Duplicate Name",
              "monitoredFolders": [{ "path": "C:\\Test2" }],
              "actions": [{ "$type": "move", "destinationPath": "C:\\Dest2" }]
            }
          ]
        }
        """;

        var result = _serializationService.ImportRules(duplicateBatchJson);

        Assert.True(result.Success);
        Assert.Equal(2, result.ImportedRules.Count);

        var rule1 = result.ImportedRules[0];
        var rule2 = result.ImportedRules[1];

        // IDs must be unique and different from source
        Assert.NotEqual(fixedId, rule1.Id);
        Assert.NotEqual(fixedId, rule2.Id);
        Assert.NotEqual(rule1.Id, rule2.Id);

        // Names must be disambiguated
        Assert.Equal("Duplicate Name", rule1.Name);
        Assert.Equal("Duplicate Name (1)", rule2.Name);

        // Both disabled
        Assert.False(rule1.IsEnabled);
        Assert.False(rule2.IsEnabled);
    }
}
