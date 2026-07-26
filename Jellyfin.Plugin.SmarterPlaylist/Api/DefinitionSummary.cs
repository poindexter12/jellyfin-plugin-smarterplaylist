using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.SmarterPlaylist.Api
{
    /// <summary>
    /// One row of the definitions list.
    /// </summary>
    /// <param name="FileName">On-disk name without extension. This is the definition's identity.</param>
    /// <param name="Name">Playlist name as it appears in Jellyfin.</param>
    /// <param name="User">User the playlist is generated for.</param>
    /// <param name="UserExists">Whether that user exists on this server.</param>
    /// <param name="RuleSummary">Short description of the rules, for example "2 groups, 5 rules".</param>
    /// <param name="MaxItems">Cap this definition applies.</param>
    /// <param name="Order">Sort order name.</param>
    /// <param name="PlaylistState">Whether the Jellyfin playlist exists.</param>
    /// <param name="PlaylistItemCount">Live item count, or <c>null</c> when no playlist exists.</param>
    /// <param name="LastRefresh">Outcome of the last refresh since the server started.</param>
    /// <param name="Diagnostics">Problems found by validating the definition now.</param>
    public sealed record DefinitionSummary(
        string FileName,
        string Name,
        string User,
        bool UserExists,
        string RuleSummary,
        int MaxItems,
        string Order,
        PlaylistState PlaylistState,
        int? PlaylistItemCount,
        RefreshStatus? LastRefresh,
        IReadOnlyList<Diagnostic> Diagnostics);
}
