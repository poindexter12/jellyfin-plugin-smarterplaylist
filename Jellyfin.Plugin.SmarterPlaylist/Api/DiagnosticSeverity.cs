namespace Jellyfin.Plugin.SmarterPlaylist.Api
{
    /// <summary>
    /// Whether a diagnostic prevents the definition from working.
    /// </summary>
    public enum DiagnosticSeverity
    {
        /// <summary>The definition cannot produce a playlist until this is fixed.</summary>
        Error,

        /// <summary>The definition will run, but probably not as intended.</summary>
        Warning
    }
}
