namespace Jellyfin.Plugin.SmarterPlaylist
{
    /// <summary>
    /// Resolves the on-disk locations of playlist definition files.
    /// </summary>
    public interface ISmarterPlaylistFileSystem
    {
        /// <summary>
        /// Gets the directory holding every playlist definition.
        /// </summary>
        string BasePath { get; }

        /// <summary>
        /// Finds the definition file for a playlist by its id.
        /// </summary>
        /// <param name="smarterPlaylistId">Id of the playlist to locate.</param>
        /// <returns>The full path of the matching definition file.</returns>
        string GetSmarterPlaylistFilePath(string smarterPlaylistId);

        /// <summary>
        /// Gets the definition files belonging to a user.
        /// </summary>
        /// <param name="userId">Id of the user whose definitions to list.</param>
        /// <returns>The full paths of the user's definition files.</returns>
        string[] GetSmarterPlaylistFilePaths(string userId);

        /// <summary>
        /// Gets every playlist definition file.
        /// </summary>
        /// <returns>The full paths of all definition files.</returns>
        string[] GetAllSmarterPlaylistFilePaths();

        /// <summary>
        /// Builds the path a playlist definition should be written to.
        /// </summary>
        /// <param name="userId">Id of the owning user.</param>
        /// <param name="playlistId">File name of the definition, without extension.</param>
        /// <returns>The full path to write the definition to.</returns>
        string GetSmarterPlaylistPath(string userId, string playlistId);
    }
}
