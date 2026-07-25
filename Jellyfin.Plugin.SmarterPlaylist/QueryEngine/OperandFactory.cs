using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.SmarterPlaylist.QueryEngine
{
    /// <summary>
    /// Projects Jellyfin library items into <see cref="Operand"/> instances for rule evaluation.
    /// </summary>
    internal static class OperandFactory
    {
        /// <summary>
        /// Builds the <see cref="Operand"/> for a library item as seen by a specific user.
        /// </summary>
        /// <remarks>
        /// The result is user-scoped: <see cref="Operand.IsPlayed"/> reflects
        /// <paramref name="user"/>'s play state, not a global one.
        /// </remarks>
        /// <param name="libraryManager">Library manager used to resolve the item's credited people.</param>
        /// <param name="userDataManager">User data manager used to resolve play state.</param>
        /// <param name="baseItem">Library item to project.</param>
        /// <param name="user">User the item is being evaluated for.</param>
        /// <returns>An operand describing <paramref name="baseItem"/>.</returns>
        public static Operand GetMediaType(ILibraryManager libraryManager, IUserDataManager userDataManager, BaseItem baseItem, User user)
        {
            // MediaBrowser.Controller is compiled without nullable annotations, so these string
            // members are null-oblivious: the compiler accepts them but Album is null on anything
            // that is not audio, and a rule such as Album/Contains would throw per item.
            var operand = new Operand(baseItem.Name ?? string.Empty);

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

            foreach (var genre in baseItem.Genres)
            {
                operand.Genres.Add(genre);
            }

            foreach (var studio in baseItem.Studios)
            {
                operand.Studios.Add(studio);
            }

            var userData = userDataManager.GetUserData(user, baseItem);
            operand.IsPlayed = userData is not null && baseItem.IsPlayed(user, userData);
            operand.CommunityRating = baseItem.CommunityRating.GetValueOrDefault();
            operand.CriticRating = baseItem.CriticRating.GetValueOrDefault();
            operand.MediaType = baseItem.MediaType.ToString();
            operand.Album = baseItem.Album ?? string.Empty;

            if (baseItem.PremiereDate.HasValue)
            {
                operand.PremiereDate = new DateTimeOffset(baseItem.PremiereDate.Value).ToUnixTimeSeconds();
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
