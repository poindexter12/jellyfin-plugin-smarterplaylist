namespace Jellyfin.Plugin.SmarterPlaylist
{
    /// <summary>
    /// The on-disk selection of a sort order for a playlist.
    /// </summary>
    public class OrderDto
    {
        /// <summary>
        /// Gets or sets the name of the sort order to apply.
        /// </summary>
        /// <remarks>
        /// Recognized values are the names in <see cref="OrderRegistry.Names"/>; anything else falls
        /// back to <see cref="NoOrder"/>. Deliberately not listed here — this comment named three of
        /// the four orders for as long as the fourth existed, which is what a second copy of a list
        /// always ends up doing.
        /// </remarks>
        public string Name { get; set; } = NoOrder.OrderName;
    }
}
