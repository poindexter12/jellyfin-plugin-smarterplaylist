using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;

namespace Jellyfin.Plugin.SmarterPlaylist
{
    /// <summary>
    /// Sorts episodes by show, then season, then episode number.
    /// </summary>
    /// <remarks>
    /// This is the order for watching a franchise one series at a time: every episode of one show in
    /// sequence, then the next show. For watching a franchise in the order it was broadcast, with
    /// series interleaved as they aired, use <see cref="PremiereDateOrder"/> instead.
    /// </remarks>
    public class SeriesEpisodeOrder : Order
    {
        /// <summary>
        /// The name used to select this order in playlist JSON.
        /// </summary>
        public const string OrderName = "Series, Season, Episode";

        /// <inheritdoc />
        public override string Name => OrderName;

        /// <inheritdoc />
        public override IEnumerable<BaseItem> OrderBy(IEnumerable<BaseItem> items)
        {
            return items
                .OrderBy(x => x is Episode e ? e.SeriesName ?? string.Empty : x.Name ?? string.Empty, System.StringComparer.Ordinal)
                .ThenBy(x => (x as Episode)?.ParentIndexNumber ?? 0)
                .ThenBy(x => (x as Episode)?.IndexNumber ?? 0)
                .ThenBy(x => x.PremiereDate);
        }
    }
}
