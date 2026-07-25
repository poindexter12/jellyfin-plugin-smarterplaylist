using System;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.SmarterPlaylist
{
    /// <summary>
    /// Reads and writes playlist definitions.
    /// </summary>
    public interface ISmarterPlaylistStore
    {
        /// <summary>
        /// Loads a single playlist definition by id.
        /// </summary>
        /// <param name="smarterPlaylistId">Id of the playlist to load.</param>
        /// <returns>The loaded definition.</returns>
        Task<SmarterPlaylistDto> GetSmarterPlaylistAsync(Guid smarterPlaylistId);

        /// <summary>
        /// Loads the playlist definitions belonging to a user.
        /// </summary>
        /// <param name="userId">Id of the user whose definitions to load.</param>
        /// <returns>The user's definitions.</returns>
        Task<SmarterPlaylistDto[]> LoadPlaylistsAsync(Guid userId);

        /// <summary>
        /// Loads every playlist definition.
        /// </summary>
        /// <returns>All definitions found on disk.</returns>
        Task<SmarterPlaylistDto[]> GetAllSmarterPlaylistsAsync();

        /// <summary>
        /// Writes a playlist definition back to disk.
        /// </summary>
        /// <param name="smarterPlaylist">Definition to save.</param>
        /// <returns>A task that completes once the definition has been written.</returns>
        Task SaveAsync(SmarterPlaylistDto smarterPlaylist);

        /// <summary>
        /// Deletes a playlist definition.
        /// </summary>
        /// <param name="userId">Id of the owning user.</param>
        /// <param name="smarterPlaylistId">File name of the definition to delete, without extension.</param>
        void Delete(Guid userId, string smarterPlaylistId);
    }
}
