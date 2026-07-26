using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.SmarterPlaylist.QueryEngine;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SmarterPlaylist.ScheduleTasks
{
    /// <summary>
    /// Scheduled task that regenerates every playlist from its definition on disk.
    /// </summary>
    /// <remarks>
    /// Applying one definition lives in <see cref="IPlaylistSynchronizer"/>, shared with the save path
    /// on the configuration page. This task's own job is the batch: reading every definition, sharing
    /// one library projection per user across them, and making sure one failure cannot stop the rest.
    /// </remarks>
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
        private readonly ISmarterPlaylistStore _plStore;
        private readonly IRefreshStatusStore _statusStore;
        private readonly IPlaylistSynchronizer _synchronizer;
        private readonly IUserDataManager _userDataManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="RefreshAllPlaylists"/> class.
        /// </summary>
        /// <param name="libraryManager">Library manager used to enumerate candidate items.</param>
        /// <param name="logger">Logger for task progress and failures.</param>
        /// <param name="playlistStore">Store the playlist definitions are read from.</param>
        /// <param name="statusStore">Records the outcome of each definition's refresh.</param>
        /// <param name="synchronizer">Applies a single definition to Jellyfin.</param>
        /// <param name="userDataManager">User data manager used to resolve play state.</param>
        public RefreshAllPlaylists(
            ILibraryManager libraryManager,
            ILogger<Plugin> logger,
            ISmarterPlaylistStore playlistStore,
            IRefreshStatusStore statusStore,
            IPlaylistSynchronizer synchronizer,
            IUserDataManager userDataManager)
        {
            _libraryManager = libraryManager;
            _logger = logger;
            _plStore = playlistStore;
            _statusStore = statusStore;
            _synchronizer = synchronizer;
            _userDataManager = userDataManager;
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

            // Every definition used to read the whole library and project every item for itself, so
            // ten playlists for one user meant ten enumerations and ten projections per run. Neither
            // depends on the definition, only on the user, so both happen once per user and the
            // result is shared.
            var candidatesByUser = new Dictionary<Guid, IReadOnlyList<PlaylistCandidate>>();

            // Which lookups a projection has to perform is the union across that user's definitions:
            // if any one of them filters on a person, credits are fetched once for the run rather
            // than once per definition that asks.
            var neededByUser = NeededMembersByUser(dtos);

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
                    var status = await _synchronizer.SyncAsync(
                        dto,
                        startedUtc,
                        (user, needed) => CandidatesFor(user, dto, needed, candidatesByUser, neededByUser),
                        cancellationToken).ConfigureAwait(false);

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
        /// Collects, per user, every member any of their definitions filters on.
        /// </summary>
        /// <param name="dtos">Every definition on disk.</param>
        /// <returns>The union of referenced members, keyed by the user name a definition names.</returns>
        private static Dictionary<string, IReadOnlySet<string>> NeededMembersByUser(
            IEnumerable<SmarterPlaylistDto> dtos)
        {
            var needed = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);

            foreach (var dto in dtos)
            {
                var key = dto.User ?? string.Empty;
                if (!needed.TryGetValue(key, out var members))
                {
                    members = new HashSet<string>(StringComparer.Ordinal);
                    needed[key] = members;
                }

                foreach (var name in dto.ExpressionSets.SelectMany(s => s.Expressions).Select(e => e.MemberName))
                {
                    ((HashSet<string>)members).Add(name);
                }
            }

            return needed;
        }

        /// <summary>
        /// Returns the flattened library for a user, projecting it the first time it is asked for.
        /// </summary>
        /// <param name="user">User whose library is wanted.</param>
        /// <param name="dto">Definition being applied, used to find that user's member union.</param>
        /// <param name="needed">Members this definition alone reads, used when the union is missing.</param>
        /// <param name="cache">Projections already made during this run.</param>
        /// <param name="neededByUser">Member unions, keyed by the user name a definition names.</param>
        /// <returns>The candidates for that user.</returns>
        private IReadOnlyList<PlaylistCandidate> CandidatesFor(
            User user,
            SmarterPlaylistDto dto,
            IReadOnlySet<string> needed,
            Dictionary<Guid, IReadOnlyList<PlaylistCandidate>> cache,
            Dictionary<string, IReadOnlySet<string>> neededByUser)
        {
            if (cache.TryGetValue(user.Id, out var candidates))
            {
                return candidates;
            }

            var members = neededByUser.TryGetValue(dto.User ?? string.Empty, out var union) ? union : needed;

            // The library items go out of scope here: everything downstream reads the flattened
            // candidates, so a whole library of Jellyfin entities is not held for the run.
            var query = new InternalItemsQuery(user) { IncludeItemTypes = _supportedItems, Recursive = true };
            candidates = OperandFactory.Project(
                _libraryManager, _userDataManager, _libraryManager.GetItemsResult(query).Items, user, members);

            cache[user.Id] = candidates;

            return candidates;
        }
    }
}
