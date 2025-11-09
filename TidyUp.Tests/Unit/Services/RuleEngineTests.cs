using FluentAssertions;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Services;
using TidyUp.Tests.Helpers;

namespace TidyUp.Tests.Unit.Services;

public class RuleEngineTests : IDisposable
{
    private readonly RuleEngine _engine;
    private readonly TestFileHelper _fileHelper;

    public RuleEngineTests()
    {
        _engine = new RuleEngine();
        _fileHelper = new TestFileHelper();
    }

    [Fact]
    public void EvaluateRule_DisabledRule_ReturnsFalse()
    {
        // Arrange
        var rule = new Rule { IsEnabled = false };
        var file = _fileHelper.CreateTestFile("test.txt");

        // Act
        var result = _engine.EvaluateRule(rule, file);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void EvaluateRule_NullRule_ReturnsFalse()
    {
        // Arrange
        var file = _fileHelper.CreateTestFile("test.txt");

        // Act
        var result = _engine.EvaluateRule(null!, file);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void EvaluateRule_RuleWithNoConditions_ReturnsTrue()
    {
        // Arrange
        var rule = new Rule { IsEnabled = true, Conditions = null };
        var file = _fileHelper.CreateTestFile("test.txt");

        // Act
        var result = _engine.EvaluateRule(rule, file);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateRule_FileNameMatches_ReturnsTrue()
    {
        // Arrange
        var rule = new Rule
        {
            IsEnabled = true,
            Conditions = new ConditionGroup
            {
                Operator = LogicOperator.And,
                Conditions =
                {
                    new FileNameCondition
                    {
                        Operator = StringOperator.Is,
                        Value = "test"
                    }
                }
            }
        };
        var file = _fileHelper.CreateTestFile("test.txt");

        // Act
        var result = _engine.EvaluateRule(rule, file);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateRule_FileNameDoesNotMatch_ReturnsFalse()
    {
        // Arrange
        var rule = new Rule
        {
            IsEnabled = true,
            Conditions = new ConditionGroup
            {
                Operator = LogicOperator.And,
                Conditions =
                {
                    new FileNameCondition
                    {
                        Operator = StringOperator.Is,
                        Value = "different"
                    }
                }
            }
        };
        var file = _fileHelper.CreateTestFile("test.txt");

        // Act
        var result = _engine.EvaluateRule(rule, file);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void EvaluateRule_ExtensionMatches_ReturnsTrue()
    {
        // Arrange
        var rule = new Rule
        {
            IsEnabled = true,
            Conditions = new ConditionGroup
            {
                Operator = LogicOperator.And,
                Conditions =
                {
                    new FileExtensionCondition
                    {
                        Operator = StringOperator.Is,
                        Value = "txt"
                    }
                }
            }
        };
        var file = _fileHelper.CreateTestFile("test.txt");

        // Act
        var result = _engine.EvaluateRule(rule, file);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateRule_AndConditions_AllMustMatch()
    {
        // Arrange
        var rule = new Rule
        {
            IsEnabled = true,
            Conditions = new ConditionGroup
            {
                Operator = LogicOperator.And,
                Conditions =
                {
                    new FileNameCondition { Operator = StringOperator.Contains, Value = "test" },
                    new FileExtensionCondition { Operator = StringOperator.Is, Value = "txt" }
                }
            }
        };
        var file = _fileHelper.CreateTestFile("test.txt");

        // Act
        var result = _engine.EvaluateRule(rule, file);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateRule_AndConditions_OneFailsAllFail()
    {
        // Arrange
        var rule = new Rule
        {
            IsEnabled = true,
            Conditions = new ConditionGroup
            {
                Operator = LogicOperator.And,
                Conditions =
                {
                    new FileNameCondition { Operator = StringOperator.Contains, Value = "test" },
                    new FileExtensionCondition { Operator = StringOperator.Is, Value = "pdf" }
                }
            }
        };
        var file = _fileHelper.CreateTestFile("test.txt");

        // Act
        var result = _engine.EvaluateRule(rule, file);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void EvaluateRule_OrConditions_AnyCanMatch()
    {
        // Arrange
        var rule = new Rule
        {
            IsEnabled = true,
            Conditions = new ConditionGroup
            {
                Operator = LogicOperator.Or,
                Conditions =
                {
                    new FileExtensionCondition { Operator = StringOperator.Is, Value = "txt" },
                    new FileExtensionCondition { Operator = StringOperator.Is, Value = "pdf" }
                }
            }
        };
        var file = _fileHelper.CreateTestFile("test.txt");

        // Act
        var result = _engine.EvaluateRule(rule, file);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateRule_FileSizeGreaterThan_MatchesCorrectly()
    {
        // Arrange
        var rule = new Rule
        {
            IsEnabled = true,
            Conditions = new ConditionGroup
            {
                Conditions =
                {
                    new FileSizeCondition
                    {
                        Operator = FileSizeCondition.SizeOperator.GreaterThan,
                        Value = 5 // bytes
                    }
                }
            }
        };
        var file = _fileHelper.CreateTestFile("test.txt", "This is more than 5 bytes");

        // Act
        var result = _engine.EvaluateRule(rule, file);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void GetMatchingRules_ReturnsOnlyEnabledRules()
    {
        // Arrange
        var file = _fileHelper.CreateTestFile("test.txt");
        var rules = new List<Rule>
        {
            new Rule { IsEnabled = true, Name = "Rule 1" },
            new Rule { IsEnabled = false, Name = "Rule 2" },
            new Rule { IsEnabled = true, Name = "Rule 3" }
        };

        // Act
        var result = _engine.GetMatchingRules(rules, file);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(r => r.Name == "Rule 1");
        result.Should().Contain(r => r.Name == "Rule 3");
        result.Should().NotContain(r => r.Name == "Rule 2");
    }

    [Fact]
    public void GetMatchingRules_SortsRulesByExecutionOrder()
    {
        // Arrange
        var file = _fileHelper.CreateTestFile("test.txt");
        var rules = new List<Rule>
        {
            new Rule { IsEnabled = true, ExecutionOrder = 3, Name = "Third" },
            new Rule { IsEnabled = true, ExecutionOrder = 1, Name = "First" },
            new Rule { IsEnabled = true, ExecutionOrder = 2, Name = "Second" }
        };

        // Act
        var result = _engine.GetMatchingRules(rules, file);

        // Assert
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("First");
        result[1].Name.Should().Be("Second");
        result[2].Name.Should().Be("Third");
    }

    [Fact]
    public void GetMatchingRules_StopsOnFirstMatch_WhenRequested()
    {
        // Arrange
        var file = _fileHelper.CreateTestFile("test.txt");
        var rules = new List<Rule>
        {
            new Rule { IsEnabled = true, ExecutionOrder = 1, Name = "First" },
            new Rule { IsEnabled = true, ExecutionOrder = 2, Name = "Second" },
            new Rule { IsEnabled = true, ExecutionOrder = 3, Name = "Third" }
        };

        // Act
        var result = _engine.GetMatchingRules(rules, file, stopOnFirstMatch: true);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("First");
    }

    [Fact]
    public void GetMatchingRules_StopsOnStopProcessingAfterMatch()
    {
        // Arrange
        var file = _fileHelper.CreateTestFile("test.txt");
        var rules = new List<Rule>
        {
            new Rule { IsEnabled = true, ExecutionOrder = 1, Name = "First" },
            new Rule { IsEnabled = true, ExecutionOrder = 2, Name = "Second", StopProcessingAfterMatch = true },
            new Rule { IsEnabled = true, ExecutionOrder = 3, Name = "Third" }
        };

        // Act
        var result = _engine.GetMatchingRules(rules, file);

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("First");
        result[1].Name.Should().Be("Second");
    }

    [Fact]
    public void GetMatchingRules_OnlyReturnsRulesThatMatch()
    {
        // Arrange
        var file = _fileHelper.CreateTestFile("test.txt");
        var rules = new List<Rule>
        {
            new Rule
            {
                IsEnabled = true,
                ExecutionOrder = 1,
                Name = "Matches",
                Conditions = new ConditionGroup
                {
                    Conditions = { new FileExtensionCondition { Operator = StringOperator.Is, Value = "txt" } }
                }
            },
            new Rule
            {
                IsEnabled = true,
                ExecutionOrder = 2,
                Name = "Does not match",
                Conditions = new ConditionGroup
                {
                    Conditions = { new FileExtensionCondition { Operator = StringOperator.Is, Value = "pdf" } }
                }
            }
        };

        // Act
        var result = _engine.GetMatchingRules(rules, file);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Matches");
    }

    [Fact]
    public void GetMatchingRules_EmptyRulesList_ReturnsEmpty()
    {
        // Arrange
        var file = _fileHelper.CreateTestFile("test.txt");
        var rules = new List<Rule>();

        // Act
        var result = _engine.GetMatchingRules(rules, file);

        // Assert
        result.Should().BeEmpty();
    }

    public void Dispose()
    {
        _fileHelper.Dispose();
    }
}
