using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Controller.Entities;

namespace Jellyfin.Plugin.SmarterPlaylist
{
    /// <summary>
    /// Sorts matched items from newest release date to oldest.
    /// </summary>
    public class PremiereDateOrderDesc : Order
    {
        /// <summary>
        /// The name used to select this order in playlist JSON.
        /// </summary>
        public const string OrderName = "Release Date Descending";

        /// <inheritdoc />
        public override string Name => OrderName;

        /// <inheritdoc />
        public override IEnumerable<BaseItem> OrderBy(IEnumerable<BaseItem> items)
        {
            return items.OrderByDescending(x => x.PremiereDate);
        }
    }
}
