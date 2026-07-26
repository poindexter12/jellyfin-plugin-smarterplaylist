using System;
using System.Collections.Concurrent;

namespace Jellyfin.Plugin.SmarterPlaylist
{
    /// <summary>
    /// In-memory implementation of <see cref="IPlaylistCoverStore"/>, registered as a singleton.
    /// </summary>
    /// <remarks>
    /// The scheduled task writes while a save from the configuration page may be writing too, so the
    /// backing store is concurrent.
    /// </remarks>
    public sealed class PlaylistCoverStore : IPlaylistCoverStore
    {
        private readonly ConcurrentDictionary<string, string> _covers =
            new(StringComparer.OrdinalIgnoreCase);

        /// <inheritdoc />
        public string? Get(string fileName) => _covers.TryGetValue(fileName, out var key) ? key : null;

        /// <inheritdoc />
        public void Record(string fileName, string coverKey) => _covers[fileName] = coverKey;

        /// <inheritdoc />
        public void Forget(string fileName) => _covers.TryRemove(fileName, out _);
    }
}
