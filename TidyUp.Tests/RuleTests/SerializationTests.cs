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
}

