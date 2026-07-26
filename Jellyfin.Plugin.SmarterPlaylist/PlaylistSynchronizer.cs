using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.SmarterPlaylist.QueryEngine;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Playlists;
using MediaBrowser.Model.Playlists;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SmarterPlaylist
{
    /// <summary>
    /// Applies a definition to Jellyfin: creates the playlist if needed, then sets its contents.
    /// </summary>
    public class PlaylistSynchronizer : IPlaylistSynchronizer
    {
        /// <summary>
        /// Item kinds a playlist may contain. Anything else in the library is ignored.
        /// </summary>
        private static readonly BaseItemKind[] _supportedItems =
            [BaseItemKind.Audio, BaseItemKind.Episode, BaseItemKind.Movie];

        private readonly ILibraryManager _libraryManager;
        private readonly ILogger<Plugin> _logger;
        private readonly IPlaylistManager _playlistManager;
        private readonly ISmarterPlaylistStore _store;
        private readonly IUserDataManager _userDataManager;
        private readonly IUserManager _userManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaylistSynchronizer"/> class.
        /// </summary>
        /// <param name="libraryManager">Enumerates candidate items.</param>
        /// <param name="logger">Logger for progress and failures.</param>
        /// <param name="playlistManager">Creates and populates playlists.</param>
        /// <param name="store">Writes the playlist id back into the definition.</param>
        /// <param name="userDataManager">Resolves play state.</param>
        /// <param name="userManager">Resolves the user a definition names.</param>
        public PlaylistSynchronizer(
            ILibraryManager libraryManager,
            ILogger<Plugin> logger,
            IPlaylistManager playlistManager,
            ISmarterPlaylistStore store,
            IUserDataManager userDataManager,
            IUserManager userManager)
        {
            _libraryManager = libraryManager;
            _logger = logger;
            _playlistManager = playlistManager;
            _store = store;
            _userDataManager = userDataManager;
            _userManager = userManager;
        }

        /// <inheritdoc />
        public async Task<RefreshStatus> SyncAsync(
            SmarterPlaylistDto dto,
            DateTime startedUtc,
            Func<User, IReadOnlySet<string>, IReadOnlyList<PlaylistCandidate>>? candidateSource,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(dto);

            var smarterPlaylist = new SmarterPlaylist(dto);

            var user = _userManager.GetUserByName(smarterPlaylist.User);
            if (user is null)
            {
                _logger.LogError("No user named {User} found, please fix playlist {Playlist}", dto.User, dto.Name);

                return Failed(
                    dto,
                    startedUtc,
                    RefreshOutcome.SkippedUnknownUser,
                    null,
                    $"No user named '{dto.User}' exists on this server.");
            }

            var playlist = await ResolveOrCreateAsync(dto, user).ConfigureAwait(false);
            if (playlist is null)
            {
                _logger.LogError("Playlist {Playlist} could not be resolved after creation", dto.Name);

                return Failed(
                    dto,
                    startedUtc,
                    RefreshOutcome.Failed,
                    "PlaylistNotResolved",
                    "The playlist could not be found in Jellyfin after being created.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var candidates = candidateSource is not null
                ? candidateSource(user, smarterPlaylist.ReferencedMembers)
                : OperandFactory.Project(
                    _libraryManager, _userDataManager, GetAllUserMedia(user), user, smarterPlaylist.ReferencedMembers);

            var filtered = smarterPlaylist.FilterPlaylistItems(candidates);

            var query = new InternalItemsQuery(user)
            {
                IncludeItemTypes = _supportedItems,
                Recursive = true
            };
            // RemoveItemFromPlaylistAsync matches entries on the item id in "N" (undashed) form.
            // Passing the dashed form matches nothing and silently clears no entries.
            var existing = playlist.GetChildren(user, false, query)
                .Select(x => x.Id)
                .ToList();

            // Rewriting the playlist queues a forced metadata refresh of it, twice, so an unchanged
            // playlist is left alone rather than churned every half hour for no reason. Order counts:
            // these are ordered playlists, and a set that matches in a different sequence still needs
            // rewriting.
            if (existing.SequenceEqual(filtered.Ids))
            {
                _logger.LogDebug("Playlist {Playlist} is already up to date", dto.Name);

                return Succeeded(dto, startedUtc, filtered);
            }

            await _playlistManager.RemoveItemFromPlaylistAsync(
                playlist.Id.ToString(),
                existing.Select(id => id.ToString("N", CultureInfo.InvariantCulture))).ConfigureAwait(false);

            await _playlistManager.AddItemToPlaylistAsync(playlist.Id, [.. filtered.Ids], user.Id).ConfigureAwait(false);

            if (filtered.Truncated)
            {
                _logger.LogInformation(
                    "Playlist {Playlist} matched {Matched} items, capped to {Applied} by MaxItems",
                    dto.Name,
                    filtered.MatchedCount,
                    filtered.Ids.Count);
            }

            return Succeeded(dto, startedUtc, filtered);
        }

        /// <summary>
        /// Finds the playlist a definition points at, creating it when there is not one yet.
        /// </summary>
        /// <remarks>
        /// A newly created playlist's id is written straight back to the definition file. Without that
        /// the next run would find no id, create a second playlist, and keep doing so.
        /// </remarks>
        /// <param name="dto">Definition being applied.</param>
        /// <param name="user">Owner of the playlist.</param>
        /// <returns>The playlist, or <c>null</c> if it could not be resolved even after creating it.</returns>
        private async Task<Playlist?> ResolveOrCreateAsync(SmarterPlaylistDto dto, User user)
        {
            var existing = FindPlaylist(user, dto.Id);
            if (existing is not null)
            {
                return existing;
            }

            _logger.LogInformation("No playlist for {Playlist} yet, creating it", dto.Name);

            var request = new PlaylistCreationRequest { Name = dto.Name, UserId = user.Id };
            var created = await _playlistManager.CreatePlaylist(request).ConfigureAwait(false);

            dto.Id = created.Id;
            await _store.SaveAsync(dto).ConfigureAwait(false);

            return FindPlaylist(user, dto.Id);
        }

        /// <summary>
        /// Finds a user's playlist whose id matches a definition's stored id.
        /// </summary>
        /// <remarks>
        /// Ids are stored in the definition without dashes, so the comparison strips them from the
        /// Jellyfin-side id before matching.
        /// </remarks>
        /// <param name="user">Owner of the playlists to search.</param>
        /// <param name="dtoId">Playlist id recorded in the definition, or <c>null</c> if not created yet.</param>
        /// <returns>The playlist, or <c>null</c>.</returns>
        private Playlist? FindPlaylist(User user, string? dtoId) =>
            dtoId is null
                ? null
                : _playlistManager.GetPlaylists(user.Id)
                    .FirstOrDefault(x => x.Id.ToString().Replace("-", string.Empty, StringComparison.Ordinal) == dtoId);

        /// <summary>
        /// Enumerates every library item a user can see that a playlist may contain.
        /// </summary>
        /// <param name="user">User whose library is enumerated.</param>
        /// <returns>The candidate items for playlist matching.</returns>
        private IReadOnlyList<BaseItem> GetAllUserMedia(User user)
        {
            var query = new InternalItemsQuery(user)
            {
                IncludeItemTypes = _supportedItems,
                Recursive = true
            };

            return _libraryManager.GetItemsResult(query).Items;
        }

        /// <summary>
        /// Builds the outcome for a definition that could not be applied.
        /// </summary>
        /// <param name="dto">Definition that failed.</param>
        /// <param name="startedUtc">When the attempt began.</param>
        /// <param name="outcome">How it ended.</param>
        /// <param name="errorType">Short machine-readable cause, when there is one.</param>
        /// <param name="message">What to show the user.</param>
        /// <returns>The outcome.</returns>
        private static RefreshStatus Failed(
            SmarterPlaylistDto dto,
            DateTime startedUtc,
            RefreshOutcome outcome,
            string? errorType,
            string message) =>
            new(dto.FileName, startedUtc, DateTime.UtcNow, outcome, null, null, errorType, message);

        /// <summary>
        /// Builds the outcome for a definition that was applied.
        /// </summary>
        /// <param name="dto">Definition that was applied.</param>
        /// <param name="startedUtc">When the attempt began.</param>
        /// <param name="filtered">What the rules selected.</param>
        /// <returns>The outcome.</returns>
        private static RefreshStatus Succeeded(SmarterPlaylistDto dto, DateTime startedUtc, FilterResult filtered) =>
            new(
                dto.FileName,
                startedUtc,
                DateTime.UtcNow,
                RefreshOutcome.Succeeded,
                filtered.MatchedCount,
                filtered.Ids.Count,
                null,
                null);
    }
}
