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
        /// Recognized values are <c>NoOrder</c>, <c>Release Date Ascending</c>, and
        /// <c>Release Date Descending</c>. Anything else falls back to <c>NoOrder</c>.
        /// </remarks>
        public string Name { get; set; } = "NoOrder";
    }
}
