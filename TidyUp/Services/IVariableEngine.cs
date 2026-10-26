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
    string Resolve(string template, FileInfo fileInfo, int? counter = null);

    /// <summary>
    /// Checks if a template string contains valid variable syntax and only recognized tokens.
    /// </summary>
    bool IsValidTemplate(string template);

    /// <summary>
    /// Returns true if the token is a recognized variable name or alias (case-insensitive).
    /// </summary>
    bool IsKnownVariable(string token);

    /// <summary>
    /// Returns any unrecognized variable tokens found in the template.
    /// </summary>
    IReadOnlyList<string> GetUnknownVariables(string template);
}
