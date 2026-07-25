namespace Jellyfin.Plugin.SmarterPlaylist
{
    /// <summary>
    /// Leaves matched items in the order the library returned them.
    /// </summary>
    public class NoOrder : Order
    {
        /// <summary>
        /// The name used to select this order in playlist JSON.
        /// </summary>
        public const string OrderName = "NoOrder";

        /// <inheritdoc />
        public override string Name => OrderName;
    }
}
