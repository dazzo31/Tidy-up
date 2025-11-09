using System.IO;
using System.Text.RegularExpressions;

namespace TidyUp.Services;

/// <summary>
/// Implementation of variable interpolation service.
/// </summary>
public class VariableEngine : IVariableEngine
{
    private static readonly Regex VariablePattern = new(@"\{([^}:]+)(?::([^}]+))?\}", RegexOptions.Compiled);

    public string Resolve(string template, FileInfo fileInfo, int? counter = null)
    {
        if (string.IsNullOrEmpty(template))
            return template;

        return VariablePattern.Replace(template, match =>
        {
            var variableName = match.Groups[1].Value.ToLowerInvariant();
            var format = match.Groups[2].Success ? match.Groups[2].Value : null;

            try
            {
                return ResolveVariable(variableName, format, fileInfo, counter);
            }
            catch
            {
                // If variable resolution fails, return the original variable syntax
                return match.Value;
            }
        });
    }

    public bool IsValidTemplate(string template)
    {
        if (string.IsNullOrEmpty(template))
            return true;

        try
        {
            var matches = VariablePattern.Matches(template);
            foreach (Match match in matches)
            {
                var variableName = match.Groups[1].Value.ToLowerInvariant();
                if (!IsKnownVariable(variableName))
                    return false;
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private string ResolveVariable(string variableName, string? format, FileInfo fileInfo, int? counter)
    {
        return variableName switch
        {
            "filename" => FormatString(Path.GetFileNameWithoutExtension(fileInfo.Name), format),
            "extension" => FormatString(fileInfo.Extension.TrimStart('.'), format),
            "fullname" => FormatString(fileInfo.Name, format),
            
            "filesize" => fileInfo.Length.ToString(),
            "filesize_kb" => (fileInfo.Length / 1024.0).ToString("F2"),
            "filesize_mb" => (fileInfo.Length / (1024.0 * 1024.0)).ToString("F2"),
            
            "created_date" => FormatDate(fileInfo.CreationTime, format ?? "yyyy-MM-dd"),
            "created_year" => fileInfo.CreationTime.Year.ToString(),
            "created_month" => fileInfo.CreationTime.Month.ToString("D2"),
            "created_day" => fileInfo.CreationTime.Day.ToString("D2"),
            "created_time" => fileInfo.CreationTime.ToString("HH:mm:ss"),
            
            "modified_date" => FormatDate(fileInfo.LastWriteTime, format ?? "yyyy-MM-dd"),
            "modified_year" => fileInfo.LastWriteTime.Year.ToString(),
            "modified_month" => fileInfo.LastWriteTime.Month.ToString("D2"),
            "modified_day" => fileInfo.LastWriteTime.Day.ToString("D2"),
            "modified_time" => fileInfo.LastWriteTime.ToString("HH:mm:ss"),
            
            "folder_path" => fileInfo.Directory?.FullName ?? string.Empty,
            "folder_name" => fileInfo.Directory?.Name ?? string.Empty,
            
            "counter" => FormatCounter(counter ?? 0, format ?? "000"),
            
            "now_date" => FormatDate(DateTime.Now, format ?? "yyyy-MM-dd"),
            "now_time" => DateTime.Now.ToString("HH:mm:ss"),
            "now_year" => DateTime.Now.Year.ToString(),
            "now_month" => DateTime.Now.Month.ToString("D2"),
            "now_day" => DateTime.Now.Day.ToString("D2"),
            
            _ => $"{{{variableName}}}" // Unknown variable, return as-is
        };
    }

    private bool IsKnownVariable(string variableName)
    {
        return variableName switch
        {
            "filename" or "extension" or "fullname" or
            "filesize" or "filesize_kb" or "filesize_mb" or
            "created_date" or "created_year" or "created_month" or "created_day" or "created_time" or
            "modified_date" or "modified_year" or "modified_month" or "modified_day" or "modified_time" or
            "folder_path" or "folder_name" or
            "counter" or
            "now_date" or "now_time" or "now_year" or "now_month" or "now_day"
                => true,
            _ => false
        };
    }

    private string FormatString(string value, string? format)
    {
        if (string.IsNullOrEmpty(format))
            return value;

        return format.ToLowerInvariant() switch
        {
            "upper" => value.ToUpperInvariant(),
            "lower" => value.ToLowerInvariant(),
            "title" => System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value.ToLower()),
            _ => value
        };
    }

    private string FormatDate(DateTime date, string format)
    {
        return date.ToString(format);
    }

    private string FormatCounter(int counter, string format)
    {
        // Format can be like "000" for padding, "D3" for decimal, etc.
        if (format.All(char.IsDigit))
        {
            // Simple padding format like "000"
            return counter.ToString($"D{format.Length}");
        }
        return counter.ToString(format);
    }
}
