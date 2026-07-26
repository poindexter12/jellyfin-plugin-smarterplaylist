using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Playlists;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SmarterPlaylist
{
    /// <summary>
    /// Gives a generated playlist a cover: either one the definition names, or a collage of what is in it.
    /// </summary>
    public class PlaylistCoverService : IPlaylistCoverService
    {
        /// <summary>
        /// How many item posters go into a generated collage.
        /// </summary>
        /// <remarks>
        /// Four, in a square, which is what Jellyfin's own collection art uses. More would make each
        /// tile too small to recognise at the size a playlist card is actually displayed.
        /// </remarks>
        private const int CollageTiles = 4;

        /// <summary>
        /// Edge length of the generated collage, in pixels.
        /// </summary>
        private const int CollageSize = 600;

        private readonly IPlaylistCoverStore _coverStore;
        private readonly IImageProcessor _imageProcessor;
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger<Plugin> _logger;
        private readonly IProviderManager _providerManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaylistCoverService"/> class.
        /// </summary>
        /// <param name="coverStore">Remembers which cover each playlist already has.</param>
        /// <param name="imageProcessor">Composes the collage.</param>
        /// <param name="libraryManager">Resolves the items whose artwork is used.</param>
        /// <param name="logger">Logger for failures.</param>
        /// <param name="providerManager">Saves the finished image onto the playlist.</param>
        public PlaylistCoverService(
            IPlaylistCoverStore coverStore,
            IImageProcessor imageProcessor,
            ILibraryManager libraryManager,
            ILogger<Plugin> logger,
            IProviderManager providerManager)
        {
            _coverStore = coverStore;
            _imageProcessor = imageProcessor;
            _libraryManager = libraryManager;
            _logger = logger;
            _providerManager = providerManager;
        }

        /// <inheritdoc />
        public async Task ApplyAsync(
            SmarterPlaylistDto dto,
            Playlist playlist,
            IReadOnlyList<Guid> itemIds,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(dto);
            ArgumentNullException.ThrowIfNull(playlist);
            ArgumentNullException.ThrowIfNull(itemIds);

            var key = CoverKey(dto, itemIds);

            // Rebuilding a cover rewrites an image file and invalidates its cache tag, so an unchanged
            // one is left alone. The key covers both what the cover is made of and which kind it is,
            // so switching between a named image and a collage still counts as a change.
            if (string.Equals(_coverStore.Get(dto.FileName), key, StringComparison.Ordinal)
                && playlist.HasImage(ImageType.Primary, 0))
            {
                return;
            }

            try
            {
                var applied = dto.Image is { Length: > 0 }
                    ? await ApplyNamedImageAsync(dto, playlist, cancellationToken).ConfigureAwait(false)
                    : await ApplyCollageAsync(dto, playlist, itemIds, cancellationToken).ConfigureAwait(false);

                if (applied)
                {
                    _coverStore.Record(dto.FileName, key);
                }
            }
            catch (Exception ex) when (ex is IOException or HttpRequestException or InvalidOperationException or ArgumentException)
            {
                // Cosmetic. A playlist with the wrong picture is worth far less than a playlist that
                // failed to refresh, so this never propagates far enough to fail the sync.
                _logger.LogWarning(ex, "Could not set a cover for playlist {Playlist}", dto.Name);
            }
        }

        /// <summary>
        /// Applies the image a definition names, from a URL or a path on the server.
        /// </summary>
        /// <param name="dto">Definition naming the image.</param>
        /// <param name="playlist">Playlist to apply it to.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><c>true</c> if an image was applied.</returns>
        private async Task<bool> ApplyNamedImageAsync(
            SmarterPlaylistDto dto,
            Playlist playlist,
            CancellationToken cancellationToken)
        {
            var image = dto.Image!;

            if (Uri.TryCreate(image, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                await _providerManager
                    .SaveImage(playlist, image, ImageType.Primary, null, cancellationToken)
                    .ConfigureAwait(false);

                return true;
            }

            if (!File.Exists(image))
            {
                _logger.LogWarning(
                    "Playlist {Playlist} names a cover image at {Image}, which is not a URL and is not a file the server can read",
                    dto.Name,
                    image);

                return false;
            }

            var stream = File.OpenRead(image);
            await using (stream.ConfigureAwait(false))
            {
                await _providerManager
                    .SaveImage(playlist, stream, MimeTypeFor(image), ImageType.Primary, null, cancellationToken)
                    .ConfigureAwait(false);
            }

            return true;
        }

        /// <summary>
        /// Builds a collage from the artwork of the playlist's first few items.
        /// </summary>
        /// <remarks>
        /// Uses the same composer Jellyfin uses for collection folders, so a generated playlist looks
        /// like the rest of the library rather than like something a plugin bolted on. Falls back to
        /// the single first poster when there is not enough artwork to fill a collage, which is both
        /// what a one-series playlist wants anyway and what happens when the server's image encoder
        /// cannot compose at all.
        /// </remarks>
        /// <param name="dto">Definition being covered.</param>
        /// <param name="playlist">Playlist to apply the cover to.</param>
        /// <param name="itemIds">The playlist's items, in playlist order.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><c>true</c> if a cover was applied.</returns>
        private async Task<bool> ApplyCollageAsync(
            SmarterPlaylistDto dto,
            Playlist playlist,
            IReadOnlyList<Guid> itemIds,
            CancellationToken cancellationToken)
        {
            var posters = PosterPaths(itemIds);
            if (posters.Count == 0)
            {
                _logger.LogDebug("No artwork among the items of {Playlist}, so it keeps whatever cover it has", dto.Name);

                return false;
            }

            if (posters.Count < CollageTiles || !_imageProcessor.SupportsImageCollageCreation)
            {
                var single = File.OpenRead(posters[0]);
                await using (single.ConfigureAwait(false))
                {
                    await _providerManager
                        .SaveImage(playlist, single, MimeTypeFor(posters[0]), ImageType.Primary, null, cancellationToken)
                        .ConfigureAwait(false);
                }

                return true;
            }

            var outputPath = Path.Join(Path.GetTempPath(), $"sp-cover-{Guid.NewGuid():N}.png");

            try
            {
                _imageProcessor.CreateImageCollage(
                    new ImageCollageOptions
                    {
                        InputPaths = posters,
                        OutputPath = outputPath,
                        Width = CollageSize,
                        Height = CollageSize
                    },
                    dto.Name);

                var stream = File.OpenRead(outputPath);
                await using (stream.ConfigureAwait(false))
                {
                    await _providerManager
                        .SaveImage(playlist, stream, "image/png", ImageType.Primary, null, cancellationToken)
                        .ConfigureAwait(false);
                }

                return true;
            }
            finally
            {
                // The collage is copied into the playlist's own image storage by SaveImage, so the
                // scratch file has no reason to outlive this call.
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
        }

        /// <summary>
        /// Collects the primary image files of the first few items in a playlist.
        /// </summary>
        /// <param name="itemIds">The playlist's items, in playlist order.</param>
        /// <returns>Paths of the artwork found, at most <see cref="CollageTiles"/> of them.</returns>
        private List<string> PosterPaths(IReadOnlyList<Guid> itemIds)
        {
            var paths = new List<string>(CollageTiles);

            foreach (var id in itemIds)
            {
                if (paths.Count == CollageTiles)
                {
                    break;
                }

                var path = _libraryManager.GetItemById(id)?.PrimaryImagePath;

                // An episode often has no artwork of its own, and a missing file is worse than a
                // missing entry: the composer would fail on the whole collage rather than skip a tile.
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    paths.Add(path);
                }
            }

            return paths;
        }

        /// <summary>
        /// Describes what a cover is made of, so an identical one is not rebuilt.
        /// </summary>
        /// <remarks>
        /// For a collage this is the items it would be built from, in order, since the cover changes
        /// exactly when they do. Hashed rather than stored whole because it is only ever compared.
        /// </remarks>
        /// <param name="dto">Definition being covered.</param>
        /// <param name="itemIds">The playlist's items, in playlist order.</param>
        /// <returns>A key that changes when the cover should.</returns>
        internal static string CoverKey(SmarterPlaylistDto dto, IReadOnlyList<Guid> itemIds)
        {
            if (dto.Image is { Length: > 0 })
            {
                return "named:" + dto.Image;
            }

            var source = string.Join(
                ',',
                itemIds.Take(CollageTiles).Select(id => id.ToString("N", CultureInfo.InvariantCulture)));

            return "collage:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
        }

        /// <summary>
        /// Guesses an image's media type from its file name.
        /// </summary>
        /// <param name="path">Path of the image.</param>
        /// <returns>The media type to save it under.</returns>
        private static string MimeTypeFor(string path) =>
            Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                _ => "image/jpeg"
            };
    }
}
