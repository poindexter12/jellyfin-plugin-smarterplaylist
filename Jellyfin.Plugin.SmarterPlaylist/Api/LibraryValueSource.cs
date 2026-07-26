using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.SmarterPlaylist.QueryEngine;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;

namespace Jellyfin.Plugin.SmarterPlaylist.Api
{
    /// <summary>
    /// Says where the values a member can take are read from, for the members whose vocabulary the
    /// library already knows.
    /// </summary>
    /// <remarks>
    /// This exists so the config page can offer real values instead of a blank text box. Typing them
    /// by hand is the plugin's most common way to build a rule that quietly matches nothing:
    /// <c>Contains</c> compares a whole element exactly and case-sensitively, so <c>"Grey"</c> never
    /// finds a director called <c>"CGP Grey"</c>, and the mistake only surfaces at the next refresh as
    /// an empty playlist with no error anywhere.
    /// <para>
    /// Every value offered has to be one the rule engine would actually match, so the sources here are
    /// the same ones <see cref="OperandFactory"/> reads: people come from the people index, everything
    /// else from the same properties of the same items. Sourcing them from anywhere else would trade
    /// one kind of value-that-never-matches for another.
    /// </para>
    /// </remarks>
    public static class LibraryValueSource
    {
        /// <summary>
        /// Members filled from the people index, and the credit that fills each one.
        /// </summary>
        private static readonly Dictionary<string, PersonKind> _people = new(StringComparer.Ordinal)
        {
            [nameof(Operand.Actors)] = PersonKind.Actor,
            [nameof(Operand.Composers)] = PersonKind.Composer,
            [nameof(Operand.Directors)] = PersonKind.Director,
            [nameof(Operand.GuestStars)] = PersonKind.GuestStar,
            [nameof(Operand.Producers)] = PersonKind.Producer,
            [nameof(Operand.Writers)] = PersonKind.Writer
        };

        /// <summary>
        /// Members read straight off each library item, and how to read them.
        /// </summary>
        /// <remarks>
        /// Plain property reads on items the caller has already materialised — no per-item lookup, which
        /// is what keeps listing values cheaper than the equivalent full operand projection.
        /// </remarks>
        private static readonly Dictionary<string, Func<BaseItem, IEnumerable<string>>> _items =
            new(StringComparer.Ordinal)
            {
                [nameof(Operand.Genres)] = item => item.Genres,
                [nameof(Operand.Studios)] = item => item.Studios,
                [nameof(Operand.Tags)] = item => item.Tags,
                [nameof(Operand.OfficialRating)] = item => One(item.OfficialRating),
                [nameof(Operand.Album)] = item => One(item.Album),
                [nameof(Operand.SeriesName)] = item => One((item as Episode)?.SeriesName),
                [nameof(Operand.SeasonName)] = item => One((item as Episode)?.SeasonName)
            };

        /// <summary>
        /// Whether the library can list the values a member takes.
        /// </summary>
        /// <param name="member">Member name, as it appears in a rule's <c>MemberName</c>.</param>
        /// <returns><c>true</c> when values can be listed for this member.</returns>
        public static bool IsSupported(string member) =>
            _people.ContainsKey(member) || _items.ContainsKey(member);

        /// <summary>
        /// Gets the credit that fills a people-backed member.
        /// </summary>
        /// <param name="member">Member name.</param>
        /// <returns>The credit, or <c>null</c> when the member is not filled from the people index.</returns>
        public static PersonKind? PersonKindFor(string member) =>
            _people.TryGetValue(member, out var kind) ? kind : null;

        /// <summary>
        /// Reads a member's values from one library item.
        /// </summary>
        /// <param name="member">Member name.</param>
        /// <param name="item">Item to read from.</param>
        /// <returns>
        /// The values this item contributes, which is empty for a member the item does not carry — a
        /// movie has no series name, and nothing has every tag.
        /// </returns>
        public static IEnumerable<string> ValuesFrom(string member, BaseItem item) =>
            _items.TryGetValue(member, out var read) ? read(item) : [];

        /// <summary>
        /// Whether a member's values come from the items rather than the people index.
        /// </summary>
        /// <param name="member">Member name.</param>
        /// <returns><c>true</c> when the member is read from library items.</returns>
        public static bool IsItemBacked(string member) => _items.ContainsKey(member);

        /// <summary>
        /// Wraps a single value as a sequence, dropping it when it is absent.
        /// </summary>
        /// <remarks>
        /// Jellyfin's entity types are compiled without nullable annotations, so these read as
        /// non-null but are not: <c>Album</c> is null on anything that is not audio, and
        /// <c>OfficialRating</c> is null on anything unrated.
        /// </remarks>
        /// <param name="value">Value to wrap.</param>
        /// <returns>The value, or nothing.</returns>
        private static IEnumerable<string> One(string? value) =>
            string.IsNullOrWhiteSpace(value) ? [] : Enumerable.Repeat(value, 1);
    }
}
