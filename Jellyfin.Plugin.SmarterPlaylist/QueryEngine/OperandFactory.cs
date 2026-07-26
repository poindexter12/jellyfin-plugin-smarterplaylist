using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.SmarterPlaylist.QueryEngine
{
    /// <summary>
    /// Projects Jellyfin library items into <see cref="Operand"/> instances for rule evaluation.
    /// </summary>
    internal static class OperandFactory
    {
        /// <summary>
        /// Members that can only be filled by asking the library for the item's credits.
        /// </summary>
        private static readonly string[] _peopleMembers =
        [
            nameof(Operand.Actors),
            nameof(Operand.Composers),
            nameof(Operand.Directors),
            nameof(Operand.GuestStars),
            nameof(Operand.Producers),
            nameof(Operand.Writers)
        ];

        /// <summary>
        /// Flattens a user's candidate items into the records a playlist is selected and sorted from.
        /// </summary>
        /// <remarks>
        /// Done once per user per refresh, not once per definition, and the Jellyfin entities are
        /// dropped as soon as it returns. <paramref name="needed"/> should be the union of the members
        /// read by every definition for that user, so a lookup runs at most once per item per refresh
        /// however many playlists ask for it.
        /// </remarks>
        /// <param name="libraryManager">Library manager used to resolve credits.</param>
        /// <param name="userDataManager">User data manager used to resolve play state.</param>
        /// <param name="items">Candidate items to flatten.</param>
        /// <param name="user">User the items are being evaluated for.</param>
        /// <param name="needed">Members the rules read, or <c>null</c> to fill everything.</param>
        /// <returns>The flattened candidates, in the order the library returned them.</returns>
        public static List<PlaylistCandidate> Project(
            ILibraryManager libraryManager,
            IUserDataManager userDataManager,
            IEnumerable<BaseItem> items,
            User user,
            IReadOnlySet<string>? needed = null)
        {
            ArgumentNullException.ThrowIfNull(items);

            var candidates = new List<PlaylistCandidate>();

            foreach (var item in items)
            {
                candidates.Add(new PlaylistCandidate(
                    item.Id,
                    item.PremiereDate,
                    GetMediaType(libraryManager, userDataManager, item, user, needed)));
            }

            return candidates;
        }

        /// <summary>
        /// Builds the <see cref="Operand"/> for a library item as seen by a specific user.
        /// </summary>
        /// <remarks>
        /// The result is user-scoped: <see cref="Operand.IsPlayed"/> reflects
        /// <paramref name="user"/>'s play state, not a global one.
        /// <para>
        /// Everything here is a plain read off an item the caller already has, except the credits and
        /// the play state, which are separate lookups per item. Those two are the entire per-item cost
        /// of evaluating a playlist, so <paramref name="needed"/> exists to skip them when no rule
        /// asks for them — which, for a definition filtering on genre or year, is both of them.
        /// </para>
        /// </remarks>
        /// <param name="libraryManager">Library manager used to resolve the item's credited people.</param>
        /// <param name="userDataManager">User data manager used to resolve play state.</param>
        /// <param name="baseItem">Library item to project.</param>
        /// <param name="user">User the item is being evaluated for.</param>
        /// <param name="needed">
        /// Members the caller's rules actually read, or <c>null</c> to fill everything. A member left
        /// unfilled keeps its default, which is safe precisely because nothing reads it.
        /// </param>
        /// <returns>An operand describing <paramref name="baseItem"/>.</returns>
        public static Operand GetMediaType(
            ILibraryManager libraryManager,
            IUserDataManager userDataManager,
            BaseItem baseItem,
            User user,
            IReadOnlySet<string>? needed = null)
        {
            // MediaBrowser.Controller is compiled without nullable annotations, so these string
            // members are null-oblivious: the compiler accepts them but Album is null on anything
            // that is not audio, and a rule such as Album/Contains would throw per item.
            var operand = new Operand(baseItem.Name ?? string.Empty);

            if (needed is null || Array.Exists(_peopleMembers, needed.Contains))
            {
                var people = libraryManager.GetPeople(baseItem);
                if (people.Count != 0)
                {
                    Fill(operand.Actors, people, PersonKind.Actor);
                    Fill(operand.Composers, people, PersonKind.Composer);
                    Fill(operand.Directors, people, PersonKind.Director);
                    Fill(operand.GuestStars, people, PersonKind.GuestStar);
                    Fill(operand.Producers, people, PersonKind.Producer);
                    Fill(operand.Writers, people, PersonKind.Writer);
                }
            }

            foreach (var genre in baseItem.Genres)
            {
                operand.Genres.Add(genre);
            }

            foreach (var studio in baseItem.Studios)
            {
                operand.Studios.Add(studio);
            }

            if (needed is null || needed.Contains(nameof(Operand.IsPlayed)))
            {
                var userData = userDataManager.GetUserData(user, baseItem);
                operand.IsPlayed = userData is not null && baseItem.IsPlayed(user, userData);
            }

            operand.CommunityRating = baseItem.CommunityRating.GetValueOrDefault();
            operand.CriticRating = baseItem.CriticRating.GetValueOrDefault();
            operand.MediaType = baseItem.MediaType.ToString();
            operand.Album = baseItem.Album ?? string.Empty;

            if (baseItem.PremiereDate.HasValue)
            {
                operand.PremiereDate = new DateTimeOffset(baseItem.PremiereDate.Value).ToUnixTimeSeconds();
            }

            // SeriesName and SeasonName live on Episode, not BaseItem, so they are only meaningful for
            // episodes. Everything else stays empty rather than null, which keeps string rules from
            // throwing on movies -- the null-oblivious trap that bit Album.
            if (baseItem is Episode episode)
            {
                operand.SeriesName = episode.SeriesName ?? string.Empty;
                operand.SeasonName = episode.SeasonName ?? string.Empty;
                operand.SeasonNumber = episode.ParentIndexNumber.GetValueOrDefault();
                operand.EpisodeNumber = episode.IndexNumber.GetValueOrDefault();
            }

            operand.ProductionYear = baseItem.ProductionYear.GetValueOrDefault();
            operand.OfficialRating = baseItem.OfficialRating ?? string.Empty;
            operand.RunTimeMinutes = baseItem.RunTimeTicks.HasValue
                ? Math.Round(TimeSpan.FromTicks(baseItem.RunTimeTicks.Value).TotalMinutes, 2)
                : 0;

            foreach (var tag in baseItem.Tags)
            {
                operand.Tags.Add(tag);
            }

            operand.DateCreated = new DateTimeOffset(baseItem.DateCreated).ToUnixTimeSeconds();
            operand.DateLastRefreshed = new DateTimeOffset(baseItem.DateLastRefreshed).ToUnixTimeSeconds();
            operand.DateLastSaved = new DateTimeOffset(baseItem.DateLastSaved).ToUnixTimeSeconds();
            operand.DateModified = new DateTimeOffset(baseItem.DateModified).ToUnixTimeSeconds();
            operand.FolderPath = baseItem.ContainingFolderPath ?? string.Empty;

            return operand;
        }

        /// <summary>
        /// Copies the names of everyone credited with a given role into a target collection.
        /// </summary>
        /// <param name="target">Collection to populate.</param>
        /// <param name="people">Credited people for the item.</param>
        /// <param name="kind">Role to filter on.</param>
        private static void Fill(Collection<string> target, IReadOnlyList<PersonInfo> people, PersonKind kind)
        {
            foreach (var name in people.Where(x => x.Type == kind).Select(x => x.Name))
            {
                target.Add(name);
            }
        }
    }
}
