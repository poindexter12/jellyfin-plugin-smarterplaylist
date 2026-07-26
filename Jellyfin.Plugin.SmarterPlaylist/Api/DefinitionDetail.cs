using System.Collections.Generic;

namespace Jellyfin.Plugin.SmarterPlaylist.Api
{
    /// <summary>
    /// A single definition, with its raw JSON and validation results.
    /// </summary>
    /// <param name="Summary">The same data the list row shows.</param>
    /// <param name="RawJson">The file's contents, pretty-printed for display.</param>
    /// <param name="SourceHash">Hash of the file as read, so a later write can detect a concurrent edit.</param>
    /// <param name="Diagnostics">Problems found by validating the definition now.</param>
    public sealed record DefinitionDetail(
        DefinitionSummary Summary,
        string RawJson,
        string SourceHash,
        IReadOnlyList<Diagnostic> Diagnostics);
}
