using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.SmarterPlaylist
{
    /// <summary>
    /// Stores playlist definitions as JSON files on disk.
    /// </summary>
    /// <param name="fileSystem">Resolves the paths definitions are read from and written to.</param>
    public class SmarterPlaylistStore(ISmarterPlaylistFileSystem fileSystem) : ISmarterPlaylistStore
    {
        private readonly ISmarterPlaylistFileSystem _fileSystem = fileSystem;

        /// <inheritdoc />
        public async Task<SmarterPlaylistDto> GetSmarterPlaylistAsync(Guid smarterPlaylistId)
        {
            var fileName = _fileSystem.GetSmarterPlaylistFilePath(smarterPlaylistId.ToString());

            return await LoadPlaylistAsync(fileName).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<SmarterPlaylistDto[]> LoadPlaylistsAsync(Guid userId)
        {
            var paths = _fileSystem.GetSmarterPlaylistFilePaths(userId.ToString());

            return await Task.WhenAll(paths.Select(LoadPlaylistAsync)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<SmarterPlaylistDto[]> GetAllSmarterPlaylistsAsync()
        {
            var paths = _fileSystem.GetAllSmarterPlaylistFilePaths();

            return await Task.WhenAll(paths.Select(LoadPlaylistAsync)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentException"><paramref name="smarterPlaylist"/> has no id set.</exception>
        public async Task SaveAsync(SmarterPlaylistDto smarterPlaylist)
        {
            ArgumentNullException.ThrowIfNull(smarterPlaylist);

            if (smarterPlaylist.Id is null)
            {
                throw new ArgumentException("Playlist Id must be set before saving", nameof(smarterPlaylist));
            }

            var filePath = _fileSystem.GetSmarterPlaylistPath(smarterPlaylist.Id, smarterPlaylist.FileName);
            var writer = File.Create(filePath);

            await using (writer.ConfigureAwait(false))
            {
                await JsonSerializer.SerializeAsync(writer, smarterPlaylist).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public void Delete(Guid userId, string smarterPlaylistId)
        {
            var filePath = _fileSystem.GetSmarterPlaylistPath(userId.ToString(), smarterPlaylistId);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        /// <summary>
        /// Deserializes a single definition file.
        /// </summary>
        /// <param name="filePath">Full path of the file to read.</param>
        /// <returns>The definition the file describes.</returns>
        /// <exception cref="InvalidOperationException">The file did not contain a playlist definition.</exception>
        private static async Task<SmarterPlaylistDto> LoadPlaylistAsync(string filePath)
        {
            var reader = File.OpenRead(filePath);

            await using (reader.ConfigureAwait(false))
            {
                return await JsonSerializer.DeserializeAsync<SmarterPlaylistDto>(reader).ConfigureAwait(false)
                    ?? throw new InvalidOperationException($"Failed to deserialize smarter playlist file '{filePath}'");
            }
        }
    }
}
