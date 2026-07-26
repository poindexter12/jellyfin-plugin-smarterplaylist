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
        /// <summary>
        /// Definitions are hand-authored, so they are written back in a readable shape rather than
        /// minified. Without this, stamping in the generated Id reflows the user's own file to a
        /// single line.
        /// </summary>
        private static readonly JsonSerializerOptions _writeOptions = new() { WriteIndented = true };

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
                await JsonSerializer.SerializeAsync(writer, smarterPlaylist, _writeOptions).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public bool Delete(string fileName)
        {
            // Resolved by enumerating the folder and matching names, never by joining the caller's
            // value onto BasePath. The value therefore never reaches the file system as a path
            // fragment, so traversal is unrepresentable rather than merely guarded against.
            var path = _fileSystem.GetAllSmarterPlaylistFilePaths()
                .FirstOrDefault(p => string.Equals(Path.GetFileNameWithoutExtension(p), fileName, StringComparison.Ordinal));

            if (path is null)
            {
                return false;
            }

            File.Delete(path);

            return true;
        }

        /// <summary>
        /// Deserializes a single definition file.
        /// </summary>
        /// <remarks>
        /// The file's own name is authoritative for <see cref="SmarterPlaylistDto.FileName"/>. That field
        /// is free text and can disagree with the name on disk; since <see cref="SaveAsync"/> writes to
        /// the path the field names, a divergent value would fork one authored definition into two files,
        /// both enumerated and both refreshing. Taking the name from disk keeps identity single-valued.
        /// </remarks>
        /// <param name="filePath">Full path of the file to read.</param>
        /// <returns>The definition the file describes.</returns>
        /// <exception cref="InvalidOperationException">The file is missing, malformed, or empty.</exception>
        private static async Task<SmarterPlaylistDto> LoadPlaylistAsync(string filePath)
        {
            SmarterPlaylistDto? dto;

            try
            {
                var reader = File.OpenRead(filePath);

                await using (reader.ConfigureAwait(false))
                {
                    dto = await JsonSerializer.DeserializeAsync<SmarterPlaylistDto>(reader).ConfigureAwait(false);
                }
            }
            catch (JsonException ex)
            {
                // The raw exception names only a byte offset, which is useless when the task is
                // iterating an unknown number of files.
                throw new InvalidOperationException($"Smarter playlist file '{filePath}' is not valid JSON: {ex.Message}", ex);
            }

            if (dto is null)
            {
                throw new InvalidOperationException($"Smarter playlist file '{filePath}' is empty");
            }

            dto.FileName = Path.GetFileNameWithoutExtension(filePath);

            return dto;
        }
    }
}
