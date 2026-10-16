namespace TidyUp.Models;

public static class AppThemes
{
    public const string System = "System";
    public const string Light = "Light";
    public const string Dark = "Dark";

    public static IReadOnlyList<string> All { get; } = [System, Light, Dark];

    /// <summary>Returns the canonical spelling, or "System" for anything unrecognised.</summary>
    public static string Normalize(string? theme) =>
        All.FirstOrDefault(t => string.Equals(t, theme?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? System;
}
