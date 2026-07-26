namespace Jellyfin.Plugin.SmarterPlaylist
{
    /// <summary>
    /// How a single playlist's last refresh ended.
    /// </summary>
    public enum RefreshOutcome
    {
        /// <summary>
        /// The playlist was rebuilt successfully.
        /// </summary>
        Succeeded,

        /// <summary>
        /// The refresh threw. The definition is on disk but is not producing a playlist.
        /// </summary>
        Failed,

        /// <summary>
        /// The definition names a user the server does not have, so it was skipped.
        /// </summary>
        SkippedUnknownUser
    }
}
