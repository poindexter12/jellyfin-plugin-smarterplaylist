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
        /// <remarks>
        /// The name is matched against the files already on disk rather than turned into a path, so a
        /// caller-supplied value cannot address anything outside the definitions folder. Deleting is the
        /// one irreversible operation the plugin exposes, which is why the name never becomes a path here.
        /// </remarks>
        /// <param name="fileName">On-disk name of the definition to delete, without extension.</param>
        /// <returns><c>true</c> if a definition was deleted; <c>false</c> if no such definition exists.</returns>
        bool Delete(string fileName);
    }
}
