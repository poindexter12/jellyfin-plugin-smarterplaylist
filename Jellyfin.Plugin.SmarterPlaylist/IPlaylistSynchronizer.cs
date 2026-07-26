using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;

namespace Jellyfin.Plugin.SmarterPlaylist
{
    /// <summary>
    /// Makes the Jellyfin playlist behind a definition match what the definition says.
    /// </summary>
    /// <remarks>
    /// One implementation, two callers: the scheduled task runs it over every definition, and saving
    /// a definition on the configuration page runs it for that one. Having a single owner of "what
    /// does this definition mean in Jellyfin" is what keeps a playlist saved from the page identical
    /// to the same playlist half an hour later.
    /// </remarks>
    public interface IPlaylistSynchronizer
    {
        /// <summary>
        /// Creates the playlist if it does not exist yet, then replaces its contents with the items
        /// the definition selects.
        /// </summary>
        /// <remarks>
        /// Creating stamps the new playlist's id into the definition file, which is what ties the two
        /// together across restarts. That write is why this cannot simply be a read-only projection.
        /// </remarks>
        /// <param name="dto">Definition to apply.</param>
        /// <param name="startedUtc">When this attempt began, for the recorded outcome.</param>
        /// <param name="candidateSource">
        /// Supplies the flattened library for a user, given the members the rules read. Lets a batch
        /// caller project once and share it; pass <c>null</c> to project for this definition alone.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>What happened, ready to record.</returns>
        Task<RefreshStatus> SyncAsync(
            SmarterPlaylistDto dto,
            DateTime startedUtc,
            Func<User, IReadOnlySet<string>, IReadOnlyList<PlaylistCandidate>>? candidateSource,
            CancellationToken cancellationToken);
    }
}
