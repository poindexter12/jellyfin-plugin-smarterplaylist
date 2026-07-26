using System.Collections.Generic;

namespace Jellyfin.Plugin.SmarterPlaylist.Api
{
    /// <summary>
    /// Everything the definitions list page needs in one request.
    /// </summary>
    /// <param name="BasePath">Directory the definitions are read from, shown so users know where to put files.</param>
    /// <param name="Definitions">One entry per definition file found.</param>
    /// <param name="LoadErrors">Files that could not be read at all, so they are visible rather than silently absent.</param>
    public sealed record DefinitionsResponse(
        string BasePath,
        IReadOnlyList<DefinitionSummary> Definitions,
        IReadOnlyList<Diagnostic> LoadErrors);
}
