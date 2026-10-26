using System.IO;
using System.Text.RegularExpressions;

namespace TidyUp.Services;

/// <summary>
/// Implementation of variable interpolation service with unified token registry,
/// case-insensitive matching, and Windows filename safe time formatting.
/// </summary>
public class VariableEngine : IVariableEngine
{
    private static readonly Regex VariablePattern = new(@"\{([^}:]+)(?::([^}]+))?\}", RegexOptions.Compiled);

    private static readonly Dictionary<string, Func<FileInfo, int?, string?, string>> VariableResolvers =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // File Information
            ["filename"] = (f, _, fmt) => FormatString(Path.GetFileNameWithoutExtension(f.Name), fmt),
            ["name"] = (f, _, fmt) => FormatString(Path.GetFileNameWithoutExtension(f.Name), fmt),
            ["fullname"] = (f, _, fmt) => FormatString(f.Name, fmt),
            ["filenamewithextension"] = (f, _, fmt) => FormatString(f.Name, fmt),
            ["extension"] = (f, _, fmt) => FormatString(f.Extension.TrimStart('.'), fmt),
            ["ext"] = (f, _, fmt) => FormatString(f.Extension.TrimStart('.'), fmt),
            ["fileextension"] = (f, _, fmt) => FormatString(f.Extension.TrimStart('.'), fmt),

            // File Size
            ["filesize"] = (f, _, _) => f.Length.ToString(),
            ["filesize_kb"] = (f, _, _) => (f.Length / 1024.0).ToString("F2"),
            ["filesizekb"] = (f, _, _) => (f.Length / 1024.0).ToString("F2"),
            ["filesize_mb"] = (f, _, _) => (f.Length / (1024.0 * 1024.0)).ToString("F2"),
            ["filesizemb"] = (f, _, _) => (f.Length / (1024.0 * 1024.0)).ToString("F2"),

            // Current Date/Time ("Now")
            ["now_date"] = (_, _, fmt) => FormatDate(DateTime.Now, fmt ?? "yyyy-MM-dd"),
            ["date"] = (_, _, fmt) => FormatDate(DateTime.Now, fmt ?? "yyyy-MM-dd"),
            ["current_date"] = (_, _, fmt) => FormatDate(DateTime.Now, fmt ?? "yyyy-MM-dd"),
            ["currentdate"] = (_, _, fmt) => FormatDate(DateTime.Now, fmt ?? "yyyy-MM-dd"),

            ["now_year"] = (_, _, _) => DateTime.Now.Year.ToString(),
            ["year"] = (_, _, _) => DateTime.Now.Year.ToString(),
            ["current_year"] = (_, _, _) => DateTime.Now.Year.ToString(),
            ["currentyear"] = (_, _, _) => DateTime.Now.Year.ToString(),

            ["now_month"] = (_, _, _) => DateTime.Now.Month.ToString("D2"),
            ["month"] = (_, _, _) => DateTime.Now.Month.ToString("D2"),
            ["current_month"] = (_, _, _) => DateTime.Now.Month.ToString("D2"),
            ["currentmonth"] = (_, _, _) => DateTime.Now.Month.ToString("D2"),

            ["now_month_name"] = (_, _, _) => DateTime.Now.ToString("MMMM"),
            ["month_name"] = (_, _, _) => DateTime.Now.ToString("MMMM"),
            ["monthname"] = (_, _, _) => DateTime.Now.ToString("MMMM"),

            ["now_day"] = (_, _, _) => DateTime.Now.Day.ToString("D2"),
            ["day"] = (_, _, _) => DateTime.Now.Day.ToString("D2"),
            ["current_day"] = (_, _, _) => DateTime.Now.Day.ToString("D2"),
            ["currentday"] = (_, _, _) => DateTime.Now.Day.ToString("D2"),

            ["now_time"] = (_, _, fmt) => FormatDate(DateTime.Now, fmt ?? "HH-mm-ss"),
            ["time"] = (_, _, fmt) => FormatDate(DateTime.Now, fmt ?? "HH-mm-ss"),
            ["current_time"] = (_, _, fmt) => FormatDate(DateTime.Now, fmt ?? "HH-mm-ss"),
            ["currenttime"] = (_, _, fmt) => FormatDate(DateTime.Now, fmt ?? "HH-mm-ss"),

            ["now_hour"] = (_, _, _) => DateTime.Now.ToString("HH"),
            ["hour"] = (_, _, _) => DateTime.Now.ToString("HH"),
            ["now_minute"] = (_, _, _) => DateTime.Now.ToString("mm"),
            ["minute"] = (_, _, _) => DateTime.Now.ToString("mm"),
            ["now_second"] = (_, _, _) => DateTime.Now.ToString("ss"),
            ["second"] = (_, _, _) => DateTime.Now.ToString("ss"),

            ["now_datetime"] = (_, _, fmt) => FormatDate(DateTime.Now, fmt ?? "yyyy-MM-dd_HH-mm-ss"),
            ["datetime"] = (_, _, fmt) => FormatDate(DateTime.Now, fmt ?? "yyyy-MM-dd_HH-mm-ss"),
            ["current_datetime"] = (_, _, fmt) => FormatDate(DateTime.Now, fmt ?? "yyyy-MM-dd_HH-mm-ss"),
            ["currentdatetime"] = (_, _, fmt) => FormatDate(DateTime.Now, fmt ?? "yyyy-MM-dd_HH-mm-ss"),

            // Creation Date/Time
            ["created_date"] = (f, _, fmt) => FormatDate(f.CreationTime, fmt ?? "yyyy-MM-dd"),
            ["creationdate"] = (f, _, fmt) => FormatDate(f.CreationTime, fmt ?? "yyyy-MM-dd"),
            ["creation_date"] = (f, _, fmt) => FormatDate(f.CreationTime, fmt ?? "yyyy-MM-dd"),

            ["created_year"] = (f, _, _) => f.CreationTime.Year.ToString(),
            ["creationyear"] = (f, _, _) => f.CreationTime.Year.ToString(),
            ["creation_year"] = (f, _, _) => f.CreationTime.Year.ToString(),

            ["created_month"] = (f, _, _) => f.CreationTime.Month.ToString("D2"),
            ["creationmonth"] = (f, _, _) => f.CreationTime.Month.ToString("D2"),
            ["creation_month"] = (f, _, _) => f.CreationTime.Month.ToString("D2"),

            ["created_month_name"] = (f, _, _) => f.CreationTime.ToString("MMMM"),
            ["creationmonthname"] = (f, _, _) => f.CreationTime.ToString("MMMM"),
            ["creation_month_name"] = (f, _, _) => f.CreationTime.ToString("MMMM"),

            ["created_day"] = (f, _, _) => f.CreationTime.Day.ToString("D2"),
            ["creationday"] = (f, _, _) => f.CreationTime.Day.ToString("D2"),
            ["creation_day"] = (f, _, _) => f.CreationTime.Day.ToString("D2"),

            ["created_time"] = (f, _, fmt) => FormatDate(f.CreationTime, fmt ?? "HH-mm-ss"),
            ["creationtime"] = (f, _, fmt) => FormatDate(f.CreationTime, fmt ?? "HH-mm-ss"),
            ["creation_time"] = (f, _, fmt) => FormatDate(f.CreationTime, fmt ?? "HH-mm-ss"),

            ["created_hour"] = (f, _, _) => f.CreationTime.ToString("HH"),
            ["creationhour"] = (f, _, _) => f.CreationTime.ToString("HH"),
            ["created_minute"] = (f, _, _) => f.CreationTime.ToString("mm"),
            ["creationminute"] = (f, _, _) => f.CreationTime.ToString("mm"),
            ["created_second"] = (f, _, _) => f.CreationTime.ToString("ss"),
            ["creationsecond"] = (f, _, _) => f.CreationTime.ToString("ss"),

            ["created_datetime"] = (f, _, fmt) => FormatDate(f.CreationTime, fmt ?? "yyyy-MM-dd_HH-mm-ss"),
            ["creationdatetime"] = (f, _, fmt) => FormatDate(f.CreationTime, fmt ?? "yyyy-MM-dd_HH-mm-ss"),

            // Modification Date/Time
            ["modified_date"] = (f, _, fmt) => FormatDate(f.LastWriteTime, fmt ?? "yyyy-MM-dd"),
            ["modifieddate"] = (f, _, fmt) => FormatDate(f.LastWriteTime, fmt ?? "yyyy-MM-dd"),
            ["modificationdate"] = (f, _, fmt) => FormatDate(f.LastWriteTime, fmt ?? "yyyy-MM-dd"),
            ["modification_date"] = (f, _, fmt) => FormatDate(f.LastWriteTime, fmt ?? "yyyy-MM-dd"),

            ["modified_year"] = (f, _, _) => f.LastWriteTime.Year.ToString(),
            ["modifiedyear"] = (f, _, _) => f.LastWriteTime.Year.ToString(),
            ["modificationyear"] = (f, _, _) => f.LastWriteTime.Year.ToString(),
            ["modification_year"] = (f, _, _) => f.LastWriteTime.Year.ToString(),

            ["modified_month"] = (f, _, _) => f.LastWriteTime.Month.ToString("D2"),
            ["modifiedmonth"] = (f, _, _) => f.LastWriteTime.Month.ToString("D2"),
            ["modificationmonth"] = (f, _, _) => f.LastWriteTime.Month.ToString("D2"),
            ["modification_month"] = (f, _, _) => f.LastWriteTime.Month.ToString("D2"),

            ["modified_month_name"] = (f, _, _) => f.LastWriteTime.ToString("MMMM"),
            ["modifiedmonthname"] = (f, _, _) => f.LastWriteTime.ToString("MMMM"),
            ["modificationmonthname"] = (f, _, _) => f.LastWriteTime.ToString("MMMM"),
            ["modification_month_name"] = (f, _, _) => f.LastWriteTime.ToString("MMMM"),

            ["modified_day"] = (f, _, _) => f.LastWriteTime.Day.ToString("D2"),
            ["modifiedday"] = (f, _, _) => f.LastWriteTime.Day.ToString("D2"),
            ["modificationday"] = (f, _, _) => f.LastWriteTime.Day.ToString("D2"),
            ["modification_day"] = (f, _, _) => f.LastWriteTime.Day.ToString("D2"),

            ["modified_time"] = (f, _, fmt) => FormatDate(f.LastWriteTime, fmt ?? "HH-mm-ss"),
            ["modifiedtime"] = (f, _, fmt) => FormatDate(f.LastWriteTime, fmt ?? "HH-mm-ss"),
            ["modificationtime"] = (f, _, fmt) => FormatDate(f.LastWriteTime, fmt ?? "HH-mm-ss"),
            ["modification_time"] = (f, _, fmt) => FormatDate(f.LastWriteTime, fmt ?? "HH-mm-ss"),

            ["modified_hour"] = (f, _, _) => f.LastWriteTime.ToString("HH"),
            ["modifiedhour"] = (f, _, _) => f.LastWriteTime.ToString("HH"),
            ["modificationhour"] = (f, _, _) => f.LastWriteTime.ToString("HH"),
            ["modified_minute"] = (f, _, _) => f.LastWriteTime.ToString("mm"),
            ["modifiedminute"] = (f, _, _) => f.LastWriteTime.ToString("mm"),
            ["modificationminute"] = (f, _, _) => f.LastWriteTime.ToString("mm"),
            ["modified_second"] = (f, _, _) => f.LastWriteTime.ToString("ss"),
            ["modifiedsecond"] = (f, _, _) => f.LastWriteTime.ToString("ss"),
            ["modificationsecond"] = (f, _, _) => f.LastWriteTime.ToString("ss"),

            ["modified_datetime"] = (f, _, fmt) => FormatDate(f.LastWriteTime, fmt ?? "yyyy-MM-dd_HH-mm-ss"),
            ["modifieddatetime"] = (f, _, fmt) => FormatDate(f.LastWriteTime, fmt ?? "yyyy-MM-dd_HH-mm-ss"),
            ["modificationdatetime"] = (f, _, fmt) => FormatDate(f.LastWriteTime, fmt ?? "yyyy-MM-dd_HH-mm-ss"),

            // Folder & Counter
            ["folder_path"] = (f, _, _) => f.Directory?.FullName ?? string.Empty,
            ["folderpath"] = (f, _, _) => f.Directory?.FullName ?? string.Empty,
            ["folder_name"] = (f, _, _) => f.Directory?.Name ?? string.Empty,
            ["foldername"] = (f, _, _) => f.Directory?.Name ?? string.Empty,
            ["counter"] = (_, c, fmt) => FormatCounter(c ?? 0, fmt ?? "000"),
        };

    public string Resolve(string template, FileInfo fileInfo, int? counter = null)
    {
        if (string.IsNullOrEmpty(template))
            return template;

        return VariablePattern.Replace(template, match =>
        {
            var variableName = match.Groups[1].Value.Trim();
            var format = match.Groups[2].Success ? match.Groups[2].Value : null;

            if (VariableResolvers.TryGetValue(variableName, out var resolver))
            {
                try
                {
                    return resolver(fileInfo, counter, format);
                }
                catch
                {
                    return match.Value;
                }
            }

            // Unknown variable: return original literal token
            return match.Value;
        });
    }

    public bool IsValidTemplate(string template)
    {
        if (string.IsNullOrEmpty(template))
            return true;

        var matches = VariablePattern.Matches(template);
        foreach (Match match in matches)
        {
            var variableName = match.Groups[1].Value.Trim();
            if (!IsKnownVariable(variableName))
                return false;
        }
        return true;
    }

    public bool IsKnownVariable(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        return VariableResolvers.ContainsKey(token.Trim());
    }

    public IReadOnlyList<string> GetUnknownVariables(string template)
    {
        if (string.IsNullOrWhiteSpace(template))
            return Array.Empty<string>();

        var unknown = new List<string>();
        var matches = VariablePattern.Matches(template);
        foreach (Match match in matches)
        {
            var token = match.Groups[1].Value.Trim();
            if (!IsKnownVariable(token))
            {
                unknown.Add(token);
            }
        }
        return unknown;
    }

    private static string FormatString(string value, string? format)
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

    private static string FormatDate(DateTime date, string format)
    {
        // Sanitize any colons in time formats to hyphens to guarantee valid Windows filenames
        var sanitizedFormat = format.Replace(':', '-');
        return date.ToString(sanitizedFormat);
    }

    private static string FormatCounter(int counter, string format)
    {
        if (format.All(char.IsDigit))
        {
            return counter.ToString($"D{format.Length}");
        }
        return counter.ToString(format);
    }
}
