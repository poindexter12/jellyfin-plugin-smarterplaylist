namespace Jellyfin.Plugin.SmarterPlaylist.Api
{
    /// <summary>
    /// A problem found in a playlist definition.
    /// </summary>
    /// <param name="Code">Stable identifier for the problem, for example <c>E01</c>.</param>
    /// <param name="Severity">Whether this blocks the definition from working.</param>
    /// <param name="Message">Human-readable description, naming the offending value where possible.</param>
    /// <param name="Path">Where in the definition the problem is, for example <c>ExpressionSets[0].Expressions[2]</c>.</param>
    public sealed record Diagnostic(string Code, DiagnosticSeverity Severity, string Message, string? Path);
}
