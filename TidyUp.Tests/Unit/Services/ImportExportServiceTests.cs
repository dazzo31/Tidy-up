using FluentAssertions;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Services;
using TidyUp.Tests.Helpers;

namespace TidyUp.Tests.Unit.Services;

public class ImportExportServiceTests : IDisposable
{
    private readonly ImportExportService _service;
    private readonly TestFileHelper _fileHelper;

    public ImportExportServiceTests()
    {
        _service = new ImportExportService();
        _fileHelper = new TestFileHelper();
    }

    [Fact]
    public async Task ExportRuleAsync_ValidRule_ReturnsValidJson()
    {
        // Arrange
        var rule = CreateTestRule();

        // Act
        var json = await _service.ExportRuleAsync(rule);

        // Assert
        json.Should().NotBeNullOrWhiteSpace();
        json.Should().Contain("\"version\"");
        json.Should().Contain("\"exportedAt\"");
        json.Should().Contain("\"rules\"");
        json.Should().Contain(rule.Name);
    }

    [Fact]
    public async Task ExportRulesAsync_MultipleRules_ReturnsValidJson()
    {
        // Arrange
        var rules = new List<Rule>
        {
            CreateTestRule("Rule 1"),
            CreateTestRule("Rule 2")
        };

        // Act
        var json = await _service.ExportRulesAsync(rules);

        // Assert
        json.Should().Contain("Rule 1");
        json.Should().Contain("Rule 2");
    }

    [Fact]
    public async Task ImportRuleAsync_ValidJson_ReturnsRule()
    {
        // Arrange
        var originalRule = CreateTestRule();
        var json = await _service.ExportRuleAsync(originalRule);

        // Act
        var importedRule = await _service.ImportRuleAsync(json);

        // Assert
        importedRule.Should().NotBeNull();
        importedRule.Name.Should().Be(originalRule.Name);
        importedRule.Description.Should().Be(originalRule.Description);
        importedRule.Id.Should().NotBe(originalRule.Id); // Should have new ID
    }

    [Fact]
    public async Task ImportRulesAsync_ValidJson_ReturnsRules()
    {
        // Arrange
        var originalRules = new List<Rule>
        {
            CreateTestRule("Rule 1"),
            CreateTestRule("Rule 2")
        };
        var json = await _service.ExportRulesAsync(originalRules);

        // Act
        var importedRules = await _service.ImportRulesAsync(json);

        // Assert
        importedRules.Should().HaveCount(2);
        importedRules.Should().Contain(r => r.Name == "Rule 1");
        importedRules.Should().Contain(r => r.Name == "Rule 2");
    }

    [Fact]
    public async Task ImportRulesAsync_ResetsIds_GeneratesNewGuids()
    {
        // Arrange
        var originalRule = CreateTestRule();
        var originalId = originalRule.Id;
        var json = await _service.ExportRuleAsync(originalRule);

        // Act
        var importedRules = await _service.ImportRulesAsync(json);

        // Assert
        importedRules.First().Id.Should().NotBe(originalId);
    }

    [Fact]
    public async Task ImportRulesAsync_ResetsTimestamps_UsesCurrentTime()
    {
        // Arrange
        var originalRule = CreateTestRule();
        originalRule.CreatedDate = DateTime.UtcNow.AddDays(-10);
        originalRule.ModifiedDate = DateTime.UtcNow.AddDays(-5);
        originalRule.LastRunDate = DateTime.UtcNow.AddDays(-1);
        var json = await _service.ExportRuleAsync(originalRule);

        // Act
        var importedRules = await _service.ImportRulesAsync(json);
        var importedRule = importedRules.First();

        // Assert
        importedRule.CreatedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        importedRule.ModifiedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        importedRule.LastRunDate.Should().BeNull();
        importedRule.FilesProcessedCount.Should().Be(0);
    }

    [Fact]
    public async Task ImportRulesAsync_InvalidJson_ThrowsException()
    {
        // Arrange
        var invalidJson = "{ invalid json }";

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _service.ImportRulesAsync(invalidJson);
        });
    }

    [Fact]
    public async Task ImportRulesAsync_EmptyRules_ThrowsException()
    {
        // Arrange
        var json = """{"version": "1.0", "exportedAt": "2025-01-01T00:00:00Z", "rules": []}""";

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _service.ImportRulesAsync(json);
        });
    }

    [Fact]
    public async Task ValidateRuleJsonAsync_ValidJson_ReturnsTrue()
    {
        // Arrange
        var rule = CreateTestRule();
        var json = await _service.ExportRuleAsync(rule);

        // Act
        var (isValid, errorMessage) = await _service.ValidateRuleJsonAsync(json);

        // Assert
        isValid.Should().BeTrue();
        errorMessage.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateRuleJsonAsync_InvalidJson_ReturnsFalse()
    {
        // Arrange
        var invalidJson = "{ invalid }";

        // Act
        var (isValid, errorMessage) = await _service.ValidateRuleJsonAsync(invalidJson);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ValidateRuleJsonAsync_EmptyJson_ReturnsFalse()
    {
        // Arrange
        var emptyJson = "";

        // Act
        var (isValid, errorMessage) = await _service.ValidateRuleJsonAsync(emptyJson);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().Contain("empty");
    }

    [Fact]
    public async Task ValidateRuleJsonAsync_MissingName_ReturnsFalse()
    {
        // Arrange
        var rule = CreateTestRule();
        rule.Name = "";
        var json = await _service.ExportRuleAsync(rule);

        // Act
        var (isValid, errorMessage) = await _service.ValidateRuleJsonAsync(json);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().Contain("name");
    }

    [Fact]
    public async Task ValidateRuleJsonAsync_NoMonitoredFolders_ReturnsFalse()
    {
        // Arrange
        var rule = CreateTestRule();
        rule.MonitoredFolders.Clear();
        var json = await _service.ExportRuleAsync(rule);

        // Act
        var (isValid, errorMessage) = await _service.ValidateRuleJsonAsync(json);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().Contain("monitored folders");
    }

    [Fact]
    public async Task ValidateRuleJsonAsync_NoActions_ReturnsFalse()
    {
        // Arrange
        var rule = CreateTestRule();
        rule.Actions.Clear();
        var json = await _service.ExportRuleAsync(rule);

        // Act
        var (isValid, errorMessage) = await _service.ValidateRuleJsonAsync(json);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().Contain("actions");
    }

    [Fact]
    public async Task ExportToFileAsync_ValidRule_CreatesFile()
    {
        // Arrange
        var rule = CreateTestRule();
        var filePath = _fileHelper.GetTestPath("export.json");

        // Act
        await _service.ExportToFileAsync(new[] { rule }, filePath);

        // Assert
        File.Exists(filePath).Should().BeTrue();
        var json = await File.ReadAllTextAsync(filePath);
        json.Should().Contain(rule.Name);
    }

    [Fact]
    public async Task ImportFromFileAsync_ValidFile_ReturnsRules()
    {
        // Arrange
        var originalRule = CreateTestRule();
        var filePath = _fileHelper.GetTestPath("export.json");
        await _service.ExportToFileAsync(new[] { originalRule }, filePath);

        // Act
        var importedRules = await _service.ImportFromFileAsync(filePath);

        // Assert
        importedRules.Should().HaveCount(1);
        importedRules.First().Name.Should().Be(originalRule.Name);
    }

    [Fact]
    public async Task ImportFromFileAsync_NonExistentFile_ThrowsException()
    {
        // Arrange
        var filePath = _fileHelper.GetTestPath("nonexistent.json");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
        {
            await _service.ImportFromFileAsync(filePath);
        });
    }

    [Fact]
    public async Task RoundTrip_ExportAndImport_PreservesData()
    {
        // Arrange
        var originalRule = CreateTestRule();
        originalRule.Conditions = new ConditionGroup
        {
            Operator = LogicOperator.And,
            Conditions =
            {
                new FileNameCondition { Operator = StringOperator.Contains, Value = "test" },
                new FileExtensionCondition { Operator = StringOperator.Is, Value = "txt" }
            }
        };

        var json = await _service.ExportRuleAsync(originalRule);

        // Act
        var importedRule = await _service.ImportRuleAsync(json);

        // Assert
        importedRule.Name.Should().Be(originalRule.Name);
        importedRule.Description.Should().Be(originalRule.Description);
        importedRule.IsEnabled.Should().Be(originalRule.IsEnabled);
        importedRule.ExecutionOrder.Should().Be(originalRule.ExecutionOrder);
        importedRule.MonitoredFolders.Should().HaveCount(originalRule.MonitoredFolders.Count);
        importedRule.Actions.Should().HaveCount(originalRule.Actions.Count);
        importedRule.Conditions.Should().NotBeNull();
    }

    private Rule CreateTestRule(string name = "Test Rule")
    {
        return new Rule
        {
            Name = name,
            Description = "Test description",
            IsEnabled = true,
            ExecutionOrder = 1,
            MonitoredFolders = 
            {
                new MonitoredFolder { Path = "C:\\Test", IncludeSubfolders = true }
            },
            Actions =
            {
                new MoveFileAction
                {
                    DestinationPath = "C:\\Destination",
                    ConflictResolution = ConflictResolution.Skip
                }
            }
        };
    }

    public void Dispose()
    {
        _fileHelper.Dispose();
    }
}
