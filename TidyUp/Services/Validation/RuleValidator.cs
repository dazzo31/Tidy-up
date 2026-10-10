using System.IO;
using TidyUp.Models.Domain;
using TidyUp.Utilities;

namespace TidyUp.Services.Validation;

/// <summary>
/// Static and runtime rule validator to detect recursive moves, source/destination folder nesting,
/// illegal filesystem paths, and cyclic cross-rule triggers.
/// </summary>
public class RuleValidator(IVariableEngine? variableEngine = null) : IRuleValidator
{
    private readonly IVariableEngine _variableEngine = variableEngine ?? new VariableEngine();

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    /// <inheritdoc />
    public RuleValidationResult ValidateRule(Rule rule)
    {
        var result = new RuleValidationResult();

        if (string.IsNullOrWhiteSpace(rule.Name))
        {
            result.Errors.Add(new RuleValidationError
            {
                RuleName = string.Empty,
                RuleId = rule.Id,
                PropertyName = nameof(rule.Name),
                ErrorMessage = "Rule name cannot be empty.",
                ErrorCode = RuleValidationErrorCode.EmptyRuleName
            });
        }

        if (rule.MonitoredFolders == null || rule.MonitoredFolders.Count == 0)
        {
            result.Errors.Add(new RuleValidationError
            {
                RuleName = rule.Name,
                RuleId = rule.Id,
                PropertyName = nameof(rule.MonitoredFolders),
                ErrorMessage = "Rule must have at least one monitored folder.",
                ErrorCode = RuleValidationErrorCode.MissingMonitoredFolders
            });
            return result;
        }

        if (rule.Actions == null || rule.Actions.Count == 0)
        {
            result.Errors.Add(new RuleValidationError
            {
                RuleName = rule.Name,
                RuleId = rule.Id,
                PropertyName = nameof(rule.Actions),
                ErrorMessage = "Rule must have at least one action.",
                ErrorCode = RuleValidationErrorCode.MissingActions
            });
            return result;
        }

        // 1. Validate each monitored folder
        foreach (var folder in rule.MonitoredFolders)
        {
            ValidatePath(folder.Path, rule, nameof(rule.MonitoredFolders), result);
        }

        // 2. Validate actions and cross-check source vs destination nesting
        foreach (var action in rule.Actions)
        {
            if (action is MoveFileAction moveAction)
            {
                ValidateDestinationAction(moveAction.DestinationPath, rule, action, result);
            }
            else if (action is CopyFileAction copyAction)
            {
                ValidateDestinationAction(copyAction.DestinationPath, rule, action, result);
            }
        }

        return result;
    }

    private void ValidateDestinationAction(
        string rawDestinationPath,
        Rule rule,
        FileAction action,
        RuleValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(rawDestinationPath))
        {
            result.Errors.Add(new RuleValidationError
            {
                RuleName = rule.Name,
                RuleId = rule.Id,
                PropertyName = "DestinationPath",
                ErrorMessage = "Destination path cannot be empty.",
                ErrorCode = RuleValidationErrorCode.EmptyPath
            });
            return;
        }

        var resolvedDest = ResolvePathForValidation(rawDestinationPath);
        ValidatePath(resolvedDest, rule, "DestinationPath", result);

        var normDest = NormalizePath(resolvedDest);

        // Check against every monitored folder in this rule
        foreach (var monitoredFolder in rule.MonitoredFolders)
        {
            if (string.IsNullOrWhiteSpace(monitoredFolder.Path))
                continue;

            var normSource = NormalizePath(monitoredFolder.Path);

            // Check A: Identical source and destination
            if (string.Equals(normDest, normSource, StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add(new RuleValidationError
                {
                    RuleName = rule.Name,
                    RuleId = rule.Id,
                    PropertyName = "DestinationPath",
                    ErrorMessage = $"Destination folder '{rawDestinationPath}' is identical to monitored source folder '{monitoredFolder.Path}'. This causes an infinite loop.",
                    ErrorCode = RuleValidationErrorCode.IdenticalSourceAndDestination
                });
            }
            // Check B: Destination is subfolder of monitored source
            else if (IsSubfolderOf(normDest, normSource))
            {
                if (monitoredFolder.IncludeSubfolders)
                {
                    result.Errors.Add(new RuleValidationError
                    {
                        RuleName = rule.Name,
                        RuleId = rule.Id,
                        PropertyName = "DestinationPath",
                        ErrorMessage = $"Destination folder '{rawDestinationPath}' is a subfolder of monitored folder '{monitoredFolder.Path}' which monitors subfolders. This causes an infinite recursive loop.",
                        ErrorCode = RuleValidationErrorCode.DestinationIsSubfolderOfMonitoredSource
                    });
                }
                else
                {
                    result.Warnings.Add(new RuleValidationWarning
                    {
                        RuleName = rule.Name,
                        RuleId = rule.Id,
                        PropertyName = "DestinationPath",
                        WarningMessage = $"Destination folder '{rawDestinationPath}' is located inside monitored folder '{monitoredFolder.Path}'."
                    });
                }
            }
        }
    }

    private static void ValidatePath(
        string path,
        Rule rule,
        string propertyName,
        RuleValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            result.Errors.Add(new RuleValidationError
            {
                RuleName = rule.Name,
                RuleId = rule.Id,
                PropertyName = propertyName,
                ErrorMessage = "Path cannot be empty.",
                ErrorCode = RuleValidationErrorCode.EmptyPath
            });
            return;
        }

        if (path.Contains(".."))
        {
            result.Errors.Add(new RuleValidationError
            {
                RuleName = rule.Name,
                RuleId = rule.Id,
                PropertyName = propertyName,
                ErrorMessage = "Path cannot contain '..' (directory traversal).",
                ErrorCode = RuleValidationErrorCode.DirectoryTraversal
            });
            return;
        }

        var invalidChars = Path.GetInvalidPathChars();
        if (path.IndexOfAny(invalidChars) >= 0)
        {
            result.Errors.Add(new RuleValidationError
            {
                RuleName = rule.Name,
                RuleId = rule.Id,
                PropertyName = propertyName,
                ErrorMessage = "Path contains invalid characters.",
                ErrorCode = RuleValidationErrorCode.InvalidPathCharacters
            });
            return;
        }

        // Check for reserved Windows device names in path segments
        var segments = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            var nameWithoutExt = Path.GetFileNameWithoutExtension(segment);
            if (ReservedNames.Contains(nameWithoutExt))
            {
                result.Errors.Add(new RuleValidationError
                {
                    RuleName = rule.Name,
                    RuleId = rule.Id,
                    PropertyName = propertyName,
                    ErrorMessage = $"'{nameWithoutExt}' is a reserved Windows device name.",
                    ErrorCode = RuleValidationErrorCode.ReservedDeviceName
                });
                return;
            }
        }

        if (path.Length > 260 && !path.StartsWith(@"\\?\"))
        {
            result.Warnings.Add(new RuleValidationWarning
            {
                RuleName = rule.Name,
                RuleId = rule.Id,
                PropertyName = propertyName,
                WarningMessage = "Path exceeds 260 characters and may require extended-length path prefix."
            });
        }
    }

    /// <inheritdoc />
    public RuleValidationResult ValidateRules(IEnumerable<Rule> rules)
    {
        var result = new RuleValidationResult();
        var ruleList = rules.ToList();

        // 1. Validate each rule individually
        foreach (var rule in ruleList)
        {
            var singleResult = ValidateRule(rule);
            result.Errors.AddRange(singleResult.Errors);
            result.Warnings.AddRange(singleResult.Warnings);
        }

        // 2. Detect cross-rule cyclic loops among enabled rules
        var enabledRules = ruleList.Where(r => r.IsEnabled).ToList();
        DetectCrossRuleCycles(enabledRules, result);

        return result;
    }

    private void DetectCrossRuleCycles(List<Rule> enabledRules, RuleValidationResult result)
    {
        // Build transition graph: Folder -> List of (DestinationFolder, Rule)
        var graph = new Dictionary<string, List<(string TargetFolder, Rule Rule)>>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in enabledRules)
        {
            var sourceFolders = rule.MonitoredFolders
                .Where(f => !string.IsNullOrWhiteSpace(f.Path))
                .Select(f => NormalizePath(f.Path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var destFolders = rule.Actions
                .OfType<MoveFileAction>()
                .Where(a => !string.IsNullOrWhiteSpace(a.DestinationPath))
                .Select(a => NormalizePath(ResolvePathForValidation(a.DestinationPath)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var src in sourceFolders)
            {
                if (!graph.ContainsKey(src))
                    graph[src] = new List<(string TargetFolder, Rule Rule)>();

                foreach (var dst in destFolders)
                {
                    // Avoid duplicate edges
                    if (!graph[src].Any(edge => edge.TargetFolder.Equals(dst, StringComparison.OrdinalIgnoreCase) && edge.Rule.Id == rule.Id))
                    {
                        graph[src].Add((dst, rule));
                    }
                }
            }
        }

        // Detect cycles using DFS with recursion stack
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var recursionStack = new List<(string Folder, Rule? TransitionRule)>();
        var reportedCycles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var startNode in graph.Keys)
        {
            if (!visited.Contains(startNode))
            {
                FindCyclesDfs(startNode, graph, visited, recursionStack, reportedCycles, result);
            }
        }
    }

    private static void FindCyclesDfs(
        string currentFolder,
        Dictionary<string, List<(string TargetFolder, Rule Rule)>> graph,
        HashSet<string> visited,
        List<(string Folder, Rule? TransitionRule)> recursionStack,
        HashSet<string> reportedCycles,
        RuleValidationResult result)
    {
        // Check if current node is already in the active recursion stack
        var existingIndex = recursionStack.FindIndex(entry => entry.Folder.Equals(currentFolder, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
        {
            // Cycle detected!
            var cycleChain = recursionStack.Skip(existingIndex).ToList();
            var cycleRules = cycleChain
                .Where(c => c.TransitionRule != null)
                .Select(c => c.TransitionRule!)
                .DistinctBy(r => r.Id)
                .ToList();

            // Create a canonical cycle fingerprint to prevent duplicate error reports
            var sortedRuleIds = string.Join(":", cycleRules.Select(r => r.Id.ToString()).OrderBy(id => id));
            if (reportedCycles.Add(sortedRuleIds))
            {
                var cycleDescription = string.Join(" -> ", cycleChain.Select(c => $"'{c.Folder}' (Rule: {c.TransitionRule?.Name ?? "start"})"));
                cycleDescription += $" -> '{currentFolder}'";

                foreach (var rule in cycleRules)
                {
                    result.Errors.Add(new RuleValidationError
                    {
                        RuleName = rule.Name,
                        RuleId = rule.Id,
                        PropertyName = "Actions",
                        ErrorMessage = $"Cross-rule cycle detected: {cycleDescription} forms an infinite loop.",
                        ErrorCode = RuleValidationErrorCode.CrossRuleCycleDetected
                    });
                }
            }
            return;
        }

        if (visited.Contains(currentFolder))
            return;

        visited.Add(currentFolder);

        if (graph.TryGetValue(currentFolder, out var neighbors))
        {
            foreach (var (targetFolder, rule) in neighbors)
            {
                recursionStack.Add((currentFolder, rule));
                FindCyclesDfs(targetFolder, graph, visited, recursionStack, reportedCycles, result);
                recursionStack.RemoveAt(recursionStack.Count - 1);
            }
        }
    }

    private string ResolvePathForValidation(string rawPath)
    {
        try
        {
            var dummyFile = new FileInfo(Path.Combine(Path.GetTempPath(), "dummy_sample.txt"));
            return _variableEngine.Resolve(rawPath, dummyFile, 1);
        }
        catch
        {
            return rawPath;
        }
    }

    public static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        try
        {
            return Path.GetFullPath(path).TrimEnd('\\', '/');
        }
        catch
        {
            return path.TrimEnd('\\', '/');
        }
    }

    public static bool IsSubfolderOf(string candidateSubfolder, string parentFolder)
    {
        if (string.IsNullOrWhiteSpace(candidateSubfolder) || string.IsNullOrWhiteSpace(parentFolder))
            return false;

        var normalizedCandidate = NormalizePath(candidateSubfolder);
        var normalizedParent = NormalizePath(parentFolder);

        if (string.Equals(normalizedCandidate, normalizedParent, StringComparison.OrdinalIgnoreCase))
            return false;

        var parentWithSlash = normalizedParent.EndsWith(Path.DirectorySeparatorChar)
            ? normalizedParent
            : normalizedParent + Path.DirectorySeparatorChar;

        return normalizedCandidate.StartsWith(parentWithSlash, StringComparison.OrdinalIgnoreCase);
    }
}

