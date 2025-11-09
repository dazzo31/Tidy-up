using System.IO;

namespace TidyUp.Services;

/// <summary>
/// Service for interpolating variables in strings (e.g., {filename}, {date}).
/// </summary>
public interface IVariableEngine
{
    /// <summary>
    /// Resolves all variables in the given template string using the provided file info.
    /// </summary>
    /// <param name="template">Template string with variables like {filename}, {date}, etc.</param>
    /// <param name="fileInfo">File to extract metadata from.</param>
    /// <param name="counter">Optional counter value for {counter} variable.</param>
    /// <returns>String with all variables replaced with actual values.</returns>
    string Resolve(string template, FileInfo fileInfo, int? counter = null);

    /// <summary>
    /// Checks if a template string contains valid variable syntax.
    /// </summary>
    bool IsValidTemplate(string template);
}
