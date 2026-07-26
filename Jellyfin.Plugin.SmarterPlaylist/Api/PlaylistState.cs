namespace Jellyfin.Plugin.SmarterPlaylist.Api
{
    /// <summary>
    /// Whether a definition's Jellyfin playlist currently exists.
    /// </summary>
    public enum PlaylistState
    {
        /// <summary>The definition has no id yet, so no playlist has been created.</summary>
        NotCreated,

        /// <summary>The definition names a playlist id that no longer resolves.</summary>
        Missing,

        /// <summary>The playlist exists.</summary>
        Ok
    }
}
