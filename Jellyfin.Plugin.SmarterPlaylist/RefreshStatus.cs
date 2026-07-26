using System;

namespace Jellyfin.Plugin.SmarterPlaylist
{
    /// <summary>
    /// What happened the last time a definition was refreshed.
    /// </summary>
    /// <param name="FileName">On-disk name of the definition, without extension.</param>
    /// <param name="StartedUtc">When the refresh began.</param>
    /// <param name="CompletedUtc">When the refresh finished, successfully or not.</param>
    /// <param name="Outcome">How it ended.</param>
    /// <param name="MatchedCount">Items the rules selected, before the <c>MaxItems</c> cap.</param>
    /// <param name="AppliedCount">Items actually placed in the playlist, after the cap.</param>
    /// <param name="ErrorType">Exception type name when <paramref name="Outcome"/> is failed.</param>
    /// <param name="ErrorMessage">Message to show the user when the refresh failed.</param>
    public sealed record RefreshStatus(
        string FileName,
        DateTime StartedUtc,
        DateTime CompletedUtc,
        RefreshOutcome Outcome,
        int? MatchedCount,
        int? AppliedCount,
        string? ErrorType,
        string? ErrorMessage);
}
