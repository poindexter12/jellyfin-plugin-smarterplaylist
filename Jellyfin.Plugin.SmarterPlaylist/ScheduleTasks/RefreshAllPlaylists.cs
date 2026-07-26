using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Playlists;
using MediaBrowser.Model.Playlists;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SmarterPlaylist.ScheduleTasks
{
    /// <summary>
    /// Scheduled task that regenerates every playlist from its definition on disk.
    /// </summary>
    public class RefreshAllPlaylists : IScheduledTask, IConfigurableScheduledTask
    {
        /// <summary>
        /// Item kinds a playlist may contain. Anything else in the library is ignored.
        /// </summary>
        private static readonly BaseItemKind[] _supportedItems =
            [BaseItemKind.Audio, BaseItemKind.Episode, BaseItemKind.Movie];

        /// <summary>
        /// How often the task runs when the user has not configured a different trigger.
        /// </summary>
        private static readonly TimeSpan _defaultInterval = TimeSpan.FromMinutes(30);

        private readonly ILibraryManager _libraryManager;
        private readonly ILogger<Plugin> _logger;
        private readonly IPlaylistManager _playlistManager;
        private readonly ISmarterPlaylistStore _plStore;
        private readonly IRefreshStatusStore _statusStore;
        private readonly IUserDataManager _userDataManager;
        private readonly IUserManager _userManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="RefreshAllPlaylists"/> class.
        /// </summary>
        /// <param name="libraryManager">Library manager used to enumerate candidate items.</param>
        /// <param name="logger">Logger for task progress and failures.</param>
        /// <param name="playlistManager">Playlist manager used to create and populate playlists.</param>
        /// <param name="playlistStore">Store the playlist definitions are read from and written to.</param>
        /// <param name="statusStore">Records the outcome of each definition's refresh.</param>
        /// <param name="userDataManager">User data manager used to resolve play state.</param>
        /// <param name="userManager">User manager used to resolve the owner of each playlist.</param>
        public RefreshAllPlaylists(
            ILibraryManager libraryManager,
            ILogger<Plugin> logger,
            IPlaylistManager playlistManager,
            ISmarterPlaylistStore playlistStore,
            IRefreshStatusStore statusStore,
            IUserDataManager userDataManager,
            IUserManager userManager)
        {
            _libraryManager = libraryManager;
            _logger = logger;
            _playlistManager = playlistManager;
            _plStore = playlistStore;
            _statusStore = statusStore;
            _userDataManager = userDataManager;
            _userManager = userManager;
        }

        /// <inheritdoc />
        public bool IsHidden => false;

        /// <inheritdoc />
        public bool IsEnabled => true;

        /// <inheritdoc />
        public bool IsLogged => true;

        /// <inheritdoc />
        public string Key => nameof(RefreshAllPlaylists);

        /// <inheritdoc />
        public string Name => "Refresh all SmarterPlaylists";

        /// <inheritdoc />
        public string Description => "Refresh all SmarterPlaylists";

        /// <inheritdoc />
        public string Category => "Library";

        /// <inheritdoc />
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return
            [
                new TaskTriggerInfo
                {
                    IntervalTicks = _defaultInterval.Ticks,
                    Type = TaskTriggerInfoType.IntervalTrigger
                }
            ];
        }

        /// <inheritdoc />
        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            var dtos = await _plStore.GetAllSmarterPlaylistsAsync().ConfigureAwait(false);

            for (var i = 0; i < dtos.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var dto = dtos[i];
                var startedUtc = DateTime.UtcNow;

                // One malformed definition must not stop the rest. Before this, any exception here --
                // an unknown member, a bad operator, an unparseable date -- propagated out and aborted
                // the entire run, so a single bad file silently froze every other playlist.
                //
                // The catch is deliberately broad: the failure modes are open-ended (anything the rule
                // engine, the JSON layer or a Jellyfin manager can throw), and narrowing it would let an
                // unanticipated type reintroduce exactly the bug this fixes. Cancellation is excluded,
                // because stopping the task is a decision from outside that must not be recorded as a
                // playlist failure or swallowed.
                try
                {
                    var status = await RefreshPlaylistAsync(dto, startedUtc).ConfigureAwait(false);
                    _statusStore.Record(status);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Failed to refresh playlist {Playlist}", dto.FileName);
                    _statusStore.Record(new RefreshStatus(
                        dto.FileName,
                        startedUtc,
                        DateTime.UtcNow,
                        RefreshOutcome.Failed,
                        null,
                        null,
                        ex.GetType().Name,
                        ex.Message));
                }

                progress?.Report((i + 1) * 100.0 / dtos.Length);
            }
        }

        /// <summary>
        /// Regenerates a single playlist, creating it in Jellyfin first if it does not exist yet.
        /// </summary>
        /// <param name="dto">Definition of the playlist to regenerate.</param>
        /// <param name="startedUtc">When this definition's refresh began.</param>
        /// <returns>The outcome, for the caller to record.</returns>
        private async Task<RefreshStatus> RefreshPlaylistAsync(SmarterPlaylistDto dto, DateTime startedUtc)
        {
            var smarterPlaylist = new SmarterPlaylist(dto);

            var user = _userManager.GetUserByName(smarterPlaylist.User);
            if (user is null)
            {
                _logger.LogError("No user named {User} found, please fix playlist {Playlist}", dto.User, dto.Name);

                return new RefreshStatus(
                    dto.FileName,
                    startedUtc,
                    DateTime.UtcNow,
                    RefreshOutcome.SkippedUnknownUser,
                    null,
                    null,
                    null,
                    $"No user named '{dto.User}' exists on this server.");
            }

            var matches = FindPlaylists(user, dto.Id);
            if (dto.Id is null || matches.Count == 0)
            {
                _logger.LogInformation("Playlist ID not set, creating new playlist {Playlist}", dto.Name);
                dto.Id = await CreateNewPlaylistAsync(dto, user).ConfigureAwait(false);
                await _plStore.SaveAsync(dto).ConfigureAwait(false);
                matches = FindPlaylists(user, dto.Id);
            }

            if (matches.Count == 0)
            {
                _logger.LogError("Playlist {Playlist} could not be resolved after creation", dto.Name);

                return new RefreshStatus(
                    dto.FileName,
                    startedUtc,
                    DateTime.UtcNow,
                    RefreshOutcome.Failed,
                    null,
                    null,
                    "PlaylistNotResolved",
                    "The playlist could not be found in Jellyfin after being created.");
            }

            var playlist = matches[0];
            var filtered = smarterPlaylist.FilterPlaylistItems(GetAllUserMedia(user), _libraryManager, _userDataManager, user);

            var query = new InternalItemsQuery(user)
            {
                IncludeItemTypes = _supportedItems,
                Recursive = true
            };
            // RemoveItemFromPlaylistAsync matches entries on the item id in "N" (undashed) form.
            // Passing the dashed form matches nothing and silently clears no entries.
            var existingIds = playlist.GetChildren(user, false, query)
                .Select(x => x.Id.ToString("N", CultureInfo.InvariantCulture))
                .ToList();

            await _playlistManager.RemoveItemFromPlaylistAsync(playlist.Id.ToString(), existingIds).ConfigureAwait(false);
            await _playlistManager.AddItemToPlaylistAsync(playlist.Id, [.. filtered.Ids], user.Id).ConfigureAwait(false);

            if (filtered.Truncated)
            {
                _logger.LogInformation(
                    "Playlist {Playlist} matched {Matched} items, capped to {Applied} by MaxItems",
                    dto.Name,
                    filtered.MatchedCount,
                    filtered.Ids.Count);
            }

            return new RefreshStatus(
                dto.FileName,
                startedUtc,
                DateTime.UtcNow,
                RefreshOutcome.Succeeded,
                filtered.MatchedCount,
                filtered.Ids.Count,
                null,
                null);
        }

        /// <summary>
        /// Finds a user's playlists whose id matches a definition's stored id.
        /// </summary>
        /// <remarks>
        /// Ids are stored in the definition without dashes, so the comparison strips them from the
        /// Jellyfin-side id before matching.
        /// </remarks>
        /// <param name="user">Owner of the playlists to search.</param>
        /// <param name="dtoId">Playlist id recorded in the definition, or <c>null</c> if not yet created.</param>
        /// <returns>The matching playlists, which is empty when the definition has no id yet.</returns>
        private List<Playlist> FindPlaylists(User user, string? dtoId)
        {
            if (dtoId is null)
            {
                return [];
            }

            return _playlistManager.GetPlaylists(user.Id)
                .Where(x => x.Id.ToString().Replace("-", string.Empty, StringComparison.Ordinal) == dtoId)
                .ToList();
        }

        /// <summary>
        /// Creates the Jellyfin playlist backing a definition.
        /// </summary>
        /// <param name="dto">Definition the playlist is created for.</param>
        /// <param name="user">User the playlist belongs to.</param>
        /// <returns>The id of the newly created playlist.</returns>
        private async Task<string> CreateNewPlaylistAsync(SmarterPlaylistDto dto, User user)
        {
            var request = new PlaylistCreationRequest
            {
                Name = dto.Name,
                UserId = user.Id
            };

            var result = await _playlistManager.CreatePlaylist(request).ConfigureAwait(false);

            return result.Id;
        }

        /// <summary>
        /// Enumerates every library item a user can see that a playlist may contain.
        /// </summary>
        /// <param name="user">User whose library is enumerated.</param>
        /// <returns>The candidate items for playlist matching.</returns>
        private IEnumerable<BaseItem> GetAllUserMedia(User user)
        {
            var query = new InternalItemsQuery(user)
            {
                IncludeItemTypes = _supportedItems,
                Recursive = true
            };

            return _libraryManager.GetItemsResult(query).Items;
        }
    }
}
