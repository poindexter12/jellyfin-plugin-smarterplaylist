using System;
using Jellyfin.Plugin.SmarterPlaylist.QueryEngine;

namespace Jellyfin.Plugin.SmarterPlaylist
{
    /// <summary>
    /// One library item, flattened into just what selecting and sorting a playlist needs.
    /// </summary>
    /// <remarks>
    /// Deliberately not a Jellyfin entity. A refresh holds every candidate in the library at once so
    /// that one projection can serve every definition for a user, and holding that many live
    /// <c>BaseItem</c> graphs to read a handful of fields off each costs far more memory than the
    /// fields are worth. Flattening also takes the filter off Jellyfin's object model entirely, which
    /// is what makes ordering and matching testable without a server.
    /// <para>
    /// <see cref="PremiereDate"/> is carried separately rather than read from <see cref="Operand"/>,
    /// which stores it as Unix seconds with zero standing in for "unknown". Sorting on that would put
    /// undated items in 1970 — after anything older, and before everything else — instead of grouping
    /// them as absent.
    /// </para>
    /// </remarks>
    /// <param name="Id">Jellyfin's id for the item, which is all the playlist ultimately stores.</param>
    /// <param name="PremiereDate">Release date, or <c>null</c> when the item has none.</param>
    /// <param name="Operand">The item as the rule engine sees it.</param>
    public sealed record PlaylistCandidate(Guid Id, DateTime? PremiereDate, Operand Operand)
    {
        /// <summary>
        /// Gets the title this item sorts under when grouping a playlist by show.
        /// </summary>
        /// <remarks>
        /// Episodes group under their series so a whole run stays together; everything else sorts
        /// under its own name.
        /// </remarks>
        public string SeriesSortTitle =>
            string.IsNullOrEmpty(Operand.SeriesName) ? Operand.Name : Operand.SeriesName;
    }
}
