using System.Collections.Generic;

namespace Jellyfin.Plugin.SmarterPlaylist.Api
{
    /// <summary>
    /// What a definition would select right now.
    /// </summary>
    /// <param name="MatchCount">Items matching the rules, before the MaxItems cap.</param>
    /// <param name="AppliedCount">Items that would actually be added, after the cap.</param>
    /// <param name="Truncated">Whether the cap discarded matches.</param>
    /// <param name="SampleTitles">The first few titles in playlist order, to sanity-check the rules.</param>
    /// <param name="ScannedCount">Library items examined, so the cost of the scan is visible.</param>
    /// <param name="ElapsedMs">How long the evaluation took.</param>
    public sealed record PreviewResponse(
        int MatchCount,
        int AppliedCount,
        bool Truncated,
        IReadOnlyList<string> SampleTitles,
        int ScannedCount,
        long ElapsedMs);
}
