using TidyUp.Models.Domain;

namespace TidyUp.Services;

public interface IImportExportService
{
    /// <summary>
    /// Exports a single rule to JSON format
    /// </summary>
    Task<string> ExportRuleAsync(Rule rule);
    
    /// <summary>
    /// Exports multiple rules to JSON format
    /// </summary>
    Task<string> ExportRulesAsync(IEnumerable<Rule> rules);
    
    /// <summary>
    /// Imports a single rule from JSON
    /// </summary>
    Task<Rule> ImportRuleAsync(string json);
    
    /// <summary>
    /// Imports multiple rules from JSON
    /// </summary>
    Task<IEnumerable<Rule>> ImportRulesAsync(string json);
    
    /// <summary>
    /// Validates that JSON contains valid rule data
    /// </summary>
    Task<(bool IsValid, string ErrorMessage)> ValidateRuleJsonAsync(string json);
    
    /// <summary>
    /// Exports rules to a file
    /// </summary>
    Task ExportToFileAsync(IEnumerable<Rule> rules, string filePath);
    
    /// <summary>
    /// Imports rules from a file
    /// </summary>
    Task<IEnumerable<Rule>> ImportFromFileAsync(string filePath);
}
