namespace Jellyfin.Plugin.SmarterPlaylist
{
    /// <summary>
    /// Remembers which cover a playlist was last given, so an unchanged one is not rebuilt.
    /// </summary>
    /// <remarks>
    /// Held in memory only, like <see cref="IRefreshStatusStore"/>. The cost of forgetting is one
    /// wasted cover rebuild per playlist after a restart; the cost of persisting would be rewriting
    /// the user's hand-authored definition file every time their library art changed, which is a far
    /// worse trade for a cosmetic feature.
    /// </remarks>
    public interface IPlaylistCoverStore
    {
        /// <summary>
        /// Gets the key describing the cover a definition currently has.
        /// </summary>
        /// <param name="fileName">On-disk name of the definition, without extension.</param>
        /// <returns>The key, or <c>null</c> if no cover has been applied since the server started.</returns>
        string? Get(string fileName);

        /// <summary>
        /// Records the cover a definition has just been given.
        /// </summary>
        /// <param name="fileName">On-disk name of the definition, without extension.</param>
        /// <param name="coverKey">Key describing what was applied.</param>
        void Record(string fileName, string coverKey);

        /// <summary>
        /// Drops what is remembered about a definition that no longer exists.
        /// </summary>
        /// <param name="fileName">On-disk name of the definition, without extension.</param>
        void Forget(string fileName);
    }
}
