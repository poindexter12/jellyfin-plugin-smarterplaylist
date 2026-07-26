using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jellyfin.Plugin.SmarterPlaylist.QueryEngine;

namespace Jellyfin.Plugin.SmarterPlaylist.Api
{
    /// <summary>
    /// Derives the filter vocabulary from <see cref="Operand"/> by reflection.
    /// </summary>
    /// <remarks>
    /// This must never be hand-maintained. <see cref="Operand"/>'s property names and CLR types are the
    /// plugin's user-facing contract, so a hand-written list would drift the moment a member is added and
    /// reintroduce exactly the discoverability problem the config page exists to solve.
    /// </remarks>
    public static class SchemaBuilder
    {
        private static readonly Type[] _numericTypes =
        [
            typeof(float), typeof(double), typeof(int), typeof(long), typeof(short), typeof(decimal)
        ];

        /// <summary>
        /// Builds the schema served to the configuration page.
        /// </summary>
        /// <returns>The filter vocabulary.</returns>
        public static SchemaResponse Build()
        {
            var members = typeof(Operand)
                .GetProperties()
                .Select(Describe)
                .OrderBy(m => m.Name, StringComparer.Ordinal)
                .ToList();

            var orders = new[]
            {
                NoOrder.OrderName,
                PremiereDateOrder.OrderName,
                PremiereDateOrderDesc.OrderName,
                SeriesEpisodeOrder.OrderName
            };
            var mediaTypes = Enum.GetNames<Jellyfin.Data.Enums.MediaType>();

            return new SchemaResponse(members, orders, mediaTypes, SmarterPlaylist.DefaultMaxItems);
        }

        /// <summary>
        /// Classifies a single <see cref="Operand"/> property.
        /// </summary>
        /// <param name="property">Property to classify.</param>
        /// <returns>The descriptor the UI renders from.</returns>
        private static MemberDescriptor Describe(System.Reflection.PropertyInfo property)
        {
            var type = property.PropertyType;
            var underlying = Nullable.GetUnderlyingType(type) ?? type;
            var name = property.Name;

            // Rows are evaluated in order; the first match wins. The date set is explicit rather than a
            // name-suffix heuristic, which would misclassify DateCreated and its siblings as plain numbers.
            if (Engine.DateMembers.Contains(name, StringComparer.Ordinal))
            {
                return new MemberDescriptor(
                    name,
                    Describe(type),
                    MemberKind.Date,
                    ComparisonOperators(),
                    true,
                    "Accepts a date such as 2020-07-01, treated as UTC, or a raw Unix timestamp. A bare year is rejected.");
            }

            if (underlying == typeof(bool))
            {
                return new MemberDescriptor(name, Describe(type), MemberKind.Boolean, ["Equal", "NotEqual"], false, null);
            }

            if (type == typeof(string) && name == nameof(Operand.MediaType))
            {
                return new MemberDescriptor(name, Describe(type), MemberKind.TextEnum, ["Equal", "NotEqual", "Equals"], false, "One of the media types Jellyfin defines.");
            }

            if (type == typeof(string))
            {
                return new MemberDescriptor(name, Describe(type), MemberKind.Text, StringOperators(), false, "Comparisons are case-sensitive.");
            }

            if (typeof(IEnumerable<string>).IsAssignableFrom(type))
            {
                return new MemberDescriptor(
                    name,
                    Describe(type),
                    MemberKind.TextList,
                    ["Contains", "MatchRegex", "NotMatchRegex"],
                    false,
                    "Contains matches a whole element exactly and is case-sensitive. Use MatchRegex for partial matches; it tests each element, and NotMatchRegex holds only when no element matches.");
            }

            if (Array.IndexOf(_numericTypes, underlying) >= 0)
            {
                // Ranges cannot be reflected, so they are curated here rather than in the page, keeping
                // every fact about a member in one place. CommunityRating is the 0-10 user score;
                // CriticRating is a 0-100 percentage -- sharing one 0-10 control would make every
                // realistic critic-rating rule unenterable.
                return name switch
                {
                    nameof(Operand.CommunityRating) => new MemberDescriptor(
                        name,
                        Describe(type),
                        MemberKind.Number,
                        ComparisonOperators(),
                        false,
                        "The community score, from 0 to 10.",
                        0,
                        10,
                        0.1),
                    nameof(Operand.CriticRating) => new MemberDescriptor(
                        name,
                        Describe(type),
                        MemberKind.Number,
                        ComparisonOperators(),
                        false,
                        "Critic ratings are a percentage from 0 to 100.",
                        0,
                        100,
                        1),
                    _ => new MemberDescriptor(name, Describe(type), MemberKind.Number, ComparisonOperators(), false, null)
                };
            }

            // Terminal fallback. A member landing here renders as unsupported rather than silently
            // offering operators that would throw at refresh time.
            return new MemberDescriptor(name, Describe(type), MemberKind.Unsupported, [], false, "This plugin cannot filter on this member yet.");
        }

        private static string[] StringOperators() =>
            ["Equal", "NotEqual", "Equals", "Contains", "StartsWith", "EndsWith", "MatchRegex", "NotMatchRegex"];

        private static string[] ComparisonOperators() =>
            ["Equal", "NotEqual", "GreaterThan", "GreaterThanOrEqual", "LessThan", "LessThanOrEqual"];

        private static string Describe(Type type) => type.ToString();

        /// <summary>
        /// Formats a Unix-seconds value as a readable UTC date for display.
        /// </summary>
        /// <param name="unixSeconds">Seconds since the Unix epoch.</param>
        /// <returns>An ISO-8601 UTC string.</returns>
        public static string FormatUnixSeconds(double unixSeconds) =>
            DateTimeOffset.FromUnixTimeSeconds((long)unixSeconds).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }
}
