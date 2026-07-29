namespace Jellyfin.Plugin.SmarterPlaylist.QueryEngine.Operators
{
    /// <summary>
    /// A problem an operator found with the value it was given.
    /// </summary>
    /// <remarks>
    /// Carries a code rather than letting the caller assign one, so an operator's diagnostics stay
    /// stable wherever they are surfaced. The API layer turns this into its own diagnostic type; the
    /// engine deliberately knows nothing about that type, which is what keeps the dependency pointing
    /// one way.
    /// </remarks>
    /// <param name="Code">Stable identifier for the problem, matching the API's diagnostic codes.</param>
    /// <param name="Message">What is wrong, phrased for the person who wrote the rule.</param>
    public sealed record RuleValueProblem(string Code, string Message);
}
