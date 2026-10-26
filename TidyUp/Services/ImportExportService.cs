using System.IO;
using TidyUp.Models.Domain;
using TidyUp.Services.Rules;

namespace TidyUp.Services;

/// <summary>
/// Legacy compatibility adapter delegating to the canonical RuleSerializationService.
/// </summary>
[Obsolete("Use IRuleSerializationService instead.")]
public class ImportExportService : IImportExportService
{
    private readonly IRuleSerializationService _serializationService;

    public ImportExportService(IRuleSerializationService? serializationService = null)
    {
        _serializationService = serializationService ?? new RuleSerializationService();
    }

    public Task<string> ExportRuleAsync(Rule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var json = _serializationService.ExportRules(new[] { rule });
        return Task.FromResult(json);
    }

    public Task<string> ExportRulesAsync(IEnumerable<Rule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var json = _serializationService.ExportRules(rules);
        return Task.FromResult(json);
    }

    public async Task<Rule> ImportRuleAsync(string json)
    {
        var rules = await ImportRulesAsync(json);
        return rules.FirstOrDefault() ?? throw new InvalidOperationException("No valid rule found in JSON");
    }

    public Task<IEnumerable<Rule>> ImportRulesAsync(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException("JSON is empty");
        }

        var validation = _serializationService.ValidatePackageSchema(json);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Invalid JSON format: {validation.ErrorMessage}");
        }

        var result = _serializationService.ImportRules(json);
        if (!result.Success || result.ImportedRules.Count == 0)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? "JSON does not contain any rules");
        }

        return Task.FromResult<IEnumerable<Rule>>(result.ImportedRules);
    }

    public Task<(bool IsValid, string ErrorMessage)> ValidateRuleJsonAsync(string json)
    {
        var validation = _serializationService.ValidatePackageSchema(json);
        return Task.FromResult((validation.IsValid, validation.ErrorMessage ?? string.Empty));
    }

    public async Task ExportToFileAsync(IEnumerable<Rule> rules, string filePath)
    {
        var json = _serializationService.ExportRules(rules);
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task<IEnumerable<Rule>> ImportFromFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        var json = await File.ReadAllTextAsync(filePath);
        return await ImportRulesAsync(json);
    }
}
