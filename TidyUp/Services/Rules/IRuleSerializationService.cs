using System.Text.Json.Serialization;
using TidyUp.Models.Domain;

namespace TidyUp.Services.Rules;

/// <summary>
/// Top-level schema envelope for exported rule packages.
/// </summary>
public class RulePackage
{
    [JsonPropertyName("$schema")]
    public string Schema { get; set; } = "https://tidyup.app/schemas/rules-v1.json";

    public string Version { get; set; } = "1.0";

    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;

    public string Application { get; set; } = "TidyUp";

    public List<Rule> Rules { get; set; } = new();
}

/// <summary>
/// Outcome of importing a rule package, containing imported rules and schema validation results.
/// </summary>
public class RuleImportResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<Rule> ImportedRules { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Service interface for versioned JSON rule serialization, schema validation, and path sanitization.
/// </summary>
public interface IRuleSerializationService
{
    /// <summary>
    /// Serializes rules into a versioned JSON package with schema header.
    /// </summary>
    string ExportRules(IEnumerable<Rule> rules);

    /// <summary>
    /// Validates schema, sanitizes paths, and imports rules in a Disabled state.
    /// </summary>
    RuleImportResult ImportRules(string json);

    /// <summary>
    /// Validates the JSON package structure and schema version without importing.
    /// </summary>
    (bool IsValid, string? ErrorMessage) ValidatePackageSchema(string json);
}

