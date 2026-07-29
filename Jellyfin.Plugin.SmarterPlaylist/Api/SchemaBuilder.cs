using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jellyfin.Plugin.SmarterPlaylist.QueryEngine;
using Jellyfin.Plugin.SmarterPlaylist.QueryEngine.Operators;

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
        /// <summary>
        /// Builds the schema served to the configuration page.
        /// </summary>
        /// <returns>The filter vocabulary.</returns>
        public static SchemaResponse Build()
        {
            // LibraryValues is stamped on afterwards rather than threaded through every Describe
            // branch: whether a member's values can be listed is a fact about the member, not about
            // the CLR type Describe is classifying, and keeping it in one place means adding a
            // listable member touches LibraryValueSource only.
            var members = typeof(Operand)
                .GetProperties()
                .Select(Describe)
                .Select(m => m with { LibraryValues = LibraryValueSource.IsSupported(m.Name) })
                .OrderBy(m => m.Name, StringComparer.Ordinal)
                .ToList();

            // Read from the registry rather than listed here, so the page cannot offer an order the
            // engine does not build, or miss one it does.
            var orders = OrderRegistry.Names;
            var mediaTypes = Enum.GetNames<Jellyfin.Data.Enums.MediaType>();

            // Distinct by name: Contains is registered twice, once for text and once for lists, but the
            // page only needs to know it takes a single value, which both agree on.
            var operators = OperatorRegistry.All
                .GroupBy(o => o.Name, StringComparer.Ordinal)
                .Select(g => new OperatorDescriptor(
                    g.Key,
                    g.First().Arity.ToString(),
                    string.Join(" ", g.Select(o => o.Notes).Where(n => !string.IsNullOrEmpty(n)).Distinct(StringComparer.Ordinal))
                        is { Length: > 0 } notes ? notes : null))
                .ToList();

            return new SchemaResponse(members, orders, mediaTypes, SmarterPlaylist.DefaultMaxItems, operators);
        }

        /// <summary>
        /// Classifies a single <see cref="Operand"/> property.
        /// </summary>
        /// <param name="property">Property to classify.</param>
        /// <returns>The descriptor the UI renders from.</returns>
        private static MemberDescriptor Describe(System.Reflection.PropertyInfo property)
        {
            var type = property.PropertyType;
            var name = property.Name;
            var kind = MemberClassifier.Classify(property);

            // Operators are read from the registry rather than listed here. A hand-kept list per kind is
            // exactly what let the page advertise one vocabulary while the engine accepted another.
            var operators = OperatorRegistry.NamesForKind(kind);

            return kind switch
            {
                MemberKind.Date => new MemberDescriptor(
                    name,
                    Describe(type),
                    kind,
                    operators,
                    true,
                    "Accepts a date such as 2020-07-01, treated as UTC, a raw Unix timestamp, or an offset from now "
                    + "such as now-30d. An offset is re-evaluated on every refresh, so it stays a moving window. "
                    + "Units: h, d, w, m, y. A bare year is rejected."),

                MemberKind.Boolean => new MemberDescriptor(name, Describe(type), kind, operators, false, null),

                MemberKind.TextEnum => new MemberDescriptor(name, Describe(type), kind, operators, false, "One of the media types Jellyfin defines."),

                MemberKind.Text => new MemberDescriptor(name, Describe(type), kind, operators, false, "Comparisons are case-sensitive unless the operator says otherwise."),

                MemberKind.TextList => new MemberDescriptor(name, Describe(type), kind, operators, false, "Rules test the elements of the list, not the list itself."),

                // Ranges cannot be reflected, so they are curated here rather than in the page, keeping
                // every fact about a member in one place. CommunityRating is the 0-10 user score;
                // CriticRating is a 0-100 percentage -- sharing one 0-10 control would make every
                // realistic critic-rating rule unenterable.
                MemberKind.Number => name switch
                {
                    nameof(Operand.CommunityRating) => new MemberDescriptor(
                        name,
                        Describe(type),
                        kind,
                        operators,
                        false,
                        "The community score, from 0 to 10.",
                        0,
                        10,
                        0.1),
                    nameof(Operand.CriticRating) => new MemberDescriptor(
                        name,
                        Describe(type),
                        kind,
                        operators,
                        false,
                        "Critic ratings are a percentage from 0 to 100.",
                        0,
                        100,
                        1),
                    _ => new MemberDescriptor(name, Describe(type), kind, operators, false, null)
                },

                // Terminal fallback. A member landing here renders as unsupported rather than silently
                // offering operators that would throw at refresh time.
                _ => new MemberDescriptor(name, Describe(type), MemberKind.Unsupported, [], false, "This plugin cannot filter on this member yet.")
            };
        }

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
