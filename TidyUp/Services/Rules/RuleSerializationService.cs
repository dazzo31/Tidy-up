using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using TidyUp.Models.Domain;

namespace TidyUp.Services.Rules;

/// <summary>
/// Service implementation for exporting and importing rules with schema versioning and safety validation.
/// </summary>
public class RuleSerializationService : IRuleSerializationService
{
    public const string CurrentSchemaUri = "https://tidyup.app/schemas/rules-v1.json";
    public const string CurrentSchemaVersion = "1.0";

    private static readonly HashSet<string> ReservedWindowsNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string ExportRules(IEnumerable<Rule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        var package = new RulePackage
        {
            Schema = CurrentSchemaUri,
            Version = CurrentSchemaVersion,
            ExportedAt = DateTime.UtcNow,
            Application = "TidyUp",
            Rules = rules.ToList()
        };

        return JsonSerializer.Serialize(package, JsonOptions);
    }

    public (bool IsValid, string? ErrorMessage) ValidatePackageSchema(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return (false, "Package JSON is empty or whitespace.");

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                return (false, "Root JSON element must be an object.");

            // Check version
            if (!root.TryGetProperty("version", out var versionProp) &&
                !root.TryGetProperty("Version", out versionProp))
            {
                return (false, "Package is missing mandatory 'version' property.");
            }

            var version = versionProp.GetString();
            if (version != CurrentSchemaVersion)
            {
                return (false, $"Unsupported schema version '{version}'. Supported version is '{CurrentSchemaVersion}'.");
            }

            // Check rules array
            if (!root.TryGetProperty("rules", out var rulesProp) &&
                !root.TryGetProperty("Rules", out rulesProp))
            {
                return (false, "Package is missing mandatory 'rules' array.");
            }

            if (rulesProp.ValueKind != JsonValueKind.Array || rulesProp.GetArrayLength() == 0)
            {
                return (false, "'rules' array must contain at least one rule definition.");
            }

            return (true, null);
        }
        catch (JsonException ex)
        {
            return (false, $"Malformed JSON: {ex.Message}");
        }
    }

    public RuleImportResult ImportRules(string json)
    {
        var validation = ValidatePackageSchema(json);
        if (!validation.IsValid)
        {
            return new RuleImportResult
            {
                Success = false,
                ErrorMessage = validation.ErrorMessage
            };
        }

        try
        {
            var package = JsonSerializer.Deserialize<RulePackage>(json, JsonOptions);
            if (package == null || package.Rules.Count == 0)
            {
                return new RuleImportResult
                {
                    Success = false,
                    ErrorMessage = "No rules could be deserialized from the package."
                };
            }

            var result = new RuleImportResult
            {
                Success = true
            };

            foreach (var rule in package.Rules)
            {
                // Safety Requirement: Imported rules ALWAYS start in Disabled state
                rule.IsEnabled = false;

                // Assign fresh ID to avoid collisions
                rule.Id = Guid.NewGuid();
                rule.CreatedDate = DateTime.UtcNow;
                rule.ModifiedDate = DateTime.UtcNow;
                rule.LastRunDate = null;
                rule.FilesProcessedCount = 0;

                // Sanitize monitored folders
                foreach (var folder in rule.MonitoredFolders)
                {
                    if (IsPathUnsafe(folder.Path, out var reason))
                    {
                        result.Warnings.Add($"Rule '{rule.Name}': Monitored folder '{folder.Path}' sanitized/flagged ({reason}).");
                    }
                }

                // Sanitize action paths
                foreach (var action in rule.Actions)
                {
                    if (action is MoveFileAction move && IsPathUnsafe(move.DestinationPath, out var moveReason))
                    {
                        result.Warnings.Add($"Rule '{rule.Name}': Move destination '{move.DestinationPath}' flagged ({moveReason}).");
                    }
                    else if (action is CopyFileAction copy && IsPathUnsafe(copy.DestinationPath, out var copyReason))
                    {
                        result.Warnings.Add($"Rule '{rule.Name}': Copy destination '{copy.DestinationPath}' flagged ({copyReason}).");
                    }
                }

                result.ImportedRules.Add(rule);
            }

            return result;
        }
        catch (Exception ex)
        {
            return new RuleImportResult
            {
                Success = false,
                ErrorMessage = $"Import failed during deserialization: {ex.Message}"
            };
        }
    }

    private static bool IsPathUnsafe(string path, out string reason)
    {
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var invalidChars = Path.GetInvalidPathChars();
        if (path.Any(c => invalidChars.Contains(c)))
        {
            reason = "contains invalid path characters";
            return true;
        }

        var segments = path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            var cleanSegment = segment.TrimEnd(':', ' ');
            var withoutExt = Path.GetFileNameWithoutExtension(cleanSegment);
            if (ReservedWindowsNames.Contains(withoutExt) || ReservedWindowsNames.Contains(cleanSegment))
            {
                reason = $"uses reserved Windows device name '{cleanSegment}'";
                return true;
            }
        }

        return false;
    }
}

