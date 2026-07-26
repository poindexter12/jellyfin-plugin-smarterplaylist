using System.Collections.Generic;
using MediaBrowser.Controller.Entities;

namespace Jellyfin.Plugin.SmarterPlaylist
{
    /// <summary>
    /// A sort order applied to the items a playlist selects.
    /// </summary>
    public abstract class Order
    {
        /// <summary>
        /// Gets the name used to select this order in playlist JSON.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Sorts the matched items.
        /// </summary>
        /// <param name="items">Items to sort.</param>
        /// <returns>The items in playlist order. The base implementation preserves the input order.</returns>
        public virtual IEnumerable<BaseItem> OrderBy(IEnumerable<BaseItem> items)
        {
            return items;
        }
    }
}
