using System.Collections.Generic;

namespace Jellyfin.Plugin.SmarterPlaylist.Api
{
    /// <summary>
    /// Returned when a definition is refused because validation failed.
    /// </summary>
    /// <param name="Diagnostics">Every problem found, so the user can fix them in one pass.</param>
    public sealed record ValidationProblem(IReadOnlyList<Diagnostic> Diagnostics);
}
