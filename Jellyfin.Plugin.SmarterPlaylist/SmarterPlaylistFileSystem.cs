using System.IO;
using System.Linq;
using MediaBrowser.Controller;

namespace Jellyfin.Plugin.SmarterPlaylist
{
    /// <summary>
    /// Locates playlist definitions under a <c>SmarterPlaylists</c> folder in the Jellyfin data directory.
    /// </summary>
    public class SmarterPlaylistFileSystem : ISmarterPlaylistFileSystem
    {
        /// <summary>
        /// Name of the folder created under the Jellyfin data directory to hold playlist definitions.
        /// </summary>
        private const string PlaylistFolderName = "SmarterPlaylists";

        /// <summary>
        /// Initializes a new instance of the <see cref="SmarterPlaylistFileSystem"/> class,
        /// creating the playlist directory if it does not already exist.
        /// </summary>
        /// <param name="serverApplicationPaths">Server paths used to locate the Jellyfin data directory.</param>
        public SmarterPlaylistFileSystem(IServerApplicationPaths serverApplicationPaths)
        {
            BasePath = Path.Combine(serverApplicationPaths.DataPath, PlaylistFolderName);

            if (!Directory.Exists(BasePath))
            {
                Directory.CreateDirectory(BasePath);
            }
        }

        /// <inheritdoc />
        public string BasePath { get; }

        /// <inheritdoc />
        public string GetSmarterPlaylistFilePath(string smarterPlaylistId)
        {
            return Directory.GetFiles(BasePath, $"{smarterPlaylistId}.json", SearchOption.AllDirectories).First();
        }

        /// <inheritdoc />
        /// <remarks>
        /// Definitions are not partitioned per user on disk, so this currently returns every file
        /// in <see cref="BasePath"/> regardless of <paramref name="userId"/>.
        /// </remarks>
        public string[] GetSmarterPlaylistFilePaths(string userId)
        {
            return Directory.GetFiles(BasePath);
        }

        /// <inheritdoc />
        public string[] GetAllSmarterPlaylistFilePaths()
        {
            return Directory.GetFiles(BasePath, "*.json", SearchOption.AllDirectories);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Definitions are not partitioned per user on disk, so <paramref name="userId"/> does not
        /// affect the returned path.
        /// </remarks>
        public string GetSmarterPlaylistPath(string userId, string playlistId)
        {
            return Path.Combine(BasePath, $"{playlistId}.json");
        }
    }
}
