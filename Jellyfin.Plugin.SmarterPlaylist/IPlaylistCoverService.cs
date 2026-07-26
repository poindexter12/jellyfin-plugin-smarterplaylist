using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Playlists;

namespace Jellyfin.Plugin.SmarterPlaylist
{
    /// <summary>
    /// Gives a generated playlist a cover image.
    /// </summary>
    /// <remarks>
    /// A generated playlist otherwise shows Jellyfin's placeholder, which makes a shelf of them
    /// indistinguishable from each other.
    /// </remarks>
    public interface IPlaylistCoverService
    {
        /// <summary>
        /// Sets the playlist's cover, unless it already has the right one.
        /// </summary>
        /// <remarks>
        /// Never throws for a cover problem. A wrong or missing picture is not worth failing a
        /// refresh over, so failures are logged and the playlist keeps whatever it had.
        /// </remarks>
        /// <param name="dto">Definition the playlist was built from.</param>
        /// <param name="playlist">The playlist to cover.</param>
        /// <param name="itemIds">The playlist's items, in playlist order.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that completes once the cover has been settled.</returns>
        Task ApplyAsync(
            SmarterPlaylistDto dto,
            Playlist playlist,
            IReadOnlyList<Guid> itemIds,
            CancellationToken cancellationToken);
    }
}
