using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.SmarterPlaylist
{
    /// <summary>
    /// The outcome of matching a playlist's rules against a library.
    /// </summary>
    /// <remarks>
    /// Both counts are kept because they answer different questions. <see cref="MatchedCount"/> is how
    /// many items the rules selected; <see cref="Ids"/> is what survived the <c>MaxItems</c> cap. Returning
    /// only the capped sequence makes truncation invisible, which is the failure mode that let
    /// <c>MaxItems</c> silently do nothing for so long.
    /// </remarks>
    /// <param name="Ids">Ids of the items to place in the playlist, in order, after the cap.</param>
    /// <param name="MatchedCount">How many items matched the rules before the cap was applied.</param>
    public sealed record FilterResult(IReadOnlyList<Guid> Ids, int MatchedCount)
    {
        /// <summary>
        /// Gets a value indicating whether the cap discarded any matches.
        /// </summary>
        public bool Truncated => MatchedCount > Ids.Count;
    }
}
