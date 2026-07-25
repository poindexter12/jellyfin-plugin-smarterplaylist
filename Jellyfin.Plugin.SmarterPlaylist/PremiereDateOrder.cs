using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Controller.Entities;

namespace Jellyfin.Plugin.SmarterPlaylist
{
    /// <summary>
    /// Sorts matched items from oldest release date to newest.
    /// </summary>
    public class PremiereDateOrder : Order
    {
        /// <summary>
        /// The name used to select this order in playlist JSON.
        /// </summary>
        public const string OrderName = "Release Date Ascending";

        /// <inheritdoc />
        public override string Name => OrderName;

        /// <inheritdoc />
        public override IEnumerable<BaseItem> OrderBy(IEnumerable<BaseItem> items)
        {
            return items.OrderBy(x => x.PremiereDate);
        }
    }
}
