using System.IO;
using System.Text.Json;
using TidyUp.Models.Domain;

namespace TidyUp.Services;

public class ImportExportService : IImportExportService
{
    private readonly JsonSerializerOptions _jsonOptions;

    public ImportExportService()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    public Task<string> ExportRuleAsync(Rule rule)
    {
        var exportData = new RuleExport
        {
            Version = "1.0",
            ExportedAt = DateTime.UtcNow,
            Rules = new List<Rule> { rule }
        };

        var json = JsonSerializer.Serialize(exportData, _jsonOptions);
        return Task.FromResult(json);
    }

    public Task<string> ExportRulesAsync(IEnumerable<Rule> rules)
    {
        var exportData = new RuleExport
        {
            Version = "1.0",
            ExportedAt = DateTime.UtcNow,
            Rules = rules.ToList()
        };

        var json = JsonSerializer.Serialize(exportData, _jsonOptions);
        return Task.FromResult(json);
    }

    public async Task<Rule> ImportRuleAsync(string json)
    {
        var rules = await ImportRulesAsync(json);
        return rules.FirstOrDefault() ?? throw new InvalidOperationException("No valid rule found in JSON");
    }

    public Task<IEnumerable<Rule>> ImportRulesAsync(string json)
    {
        try
        {
            var exportData = JsonSerializer.Deserialize<RuleExport>(json, _jsonOptions);
            
            if (exportData == null || exportData.Rules == null || !exportData.Rules.Any())
            {
                throw new InvalidOperationException("JSON does not contain any rules");
            }

            // Reset IDs and timestamps so they get new values when saved to database
            foreach (var rule in exportData.Rules)
            {
                rule.Id = Guid.NewGuid();
                rule.CreatedDate = DateTime.UtcNow;
                rule.ModifiedDate = DateTime.UtcNow;
                rule.LastRunDate = null;
                rule.FilesProcessedCount = 0;
            }

            return Task.FromResult<IEnumerable<Rule>>(exportData.Rules);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Invalid JSON format: {ex.Message}", ex);
        }
    }

    public Task<(bool IsValid, string ErrorMessage)> ValidateRuleJsonAsync(string json)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return Task.FromResult((false, "JSON is empty"));
            }

            var exportData = JsonSerializer.Deserialize<RuleExport>(json, _jsonOptions);
            
            if (exportData == null)
            {
                return Task.FromResult((false, "JSON could not be parsed"));
            }

            if (exportData.Rules == null || !exportData.Rules.Any())
            {
                return Task.FromResult((false, "No rules found in JSON"));
            }

            // Validate each rule has required fields
            foreach (var rule in exportData.Rules)
            {
                if (string.IsNullOrWhiteSpace(rule.Name))
                {
                    return Task.FromResult((false, "One or more rules are missing a name"));
                }

                if (rule.MonitoredFolders == null || !rule.MonitoredFolders.Any())
                {
                    return Task.FromResult((false, $"Rule '{rule.Name}' has no monitored folders"));
                }

                if (rule.Actions == null || !rule.Actions.Any())
                {
                    return Task.FromResult((false, $"Rule '{rule.Name}' has no actions"));
                }
            }

            return Task.FromResult((true, string.Empty));
        }
        catch (JsonException ex)
        {
            return Task.FromResult((false, $"Invalid JSON: {ex.Message}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult((false, $"Validation error: {ex.Message}"));
        }
    }

    public async Task ExportToFileAsync(IEnumerable<Rule> rules, string filePath)
    {
        var json = await ExportRulesAsync(rules);
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

    private class RuleExport
    {
        public string Version { get; set; } = "1.0";
        public DateTime ExportedAt { get; set; }
        public List<Rule> Rules { get; set; } = new();
    }
}
