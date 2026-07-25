using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.SmarterPlaylist
{
    /// <summary>
    /// The on-disk shape of a playlist definition, as written by the user in JSON.
    /// </summary>
    /// <remarks>
    /// Property names here are the JSON keys users author by hand, so renaming one breaks
    /// every existing playlist file. Populate handling is required because the collection
    /// properties are get-only; without it the deserializer silently skips them.
    /// </remarks>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public class SmarterPlaylistDto
    {
        /// <summary>
        /// Gets or sets the id of the generated Jellyfin playlist.
        /// </summary>
        /// <remarks>
        /// Written back by the plugin the first time the playlist is created; users leave this unset.
        /// </remarks>
        public string? Id { get; set; }

        /// <summary>
        /// Gets or sets the playlist name as it appears in Jellyfin.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the definition's own file name, without the <c>.json</c> extension.
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of the user the playlist is generated for.
        /// </summary>
        public string User { get; set; } = string.Empty;

        /// <summary>
        /// Gets the rule sets that select items for the playlist.
        /// </summary>
        /// <remarks>
        /// Sets are OR'd together; the rules within a set are AND'd. An item is included when
        /// it satisfies every rule of at least one set.
        /// </remarks>
        public Collection<ExpressionSet> ExpressionSets { get; } = [];

        /// <summary>
        /// Gets or sets the maximum number of items to include, or zero to use the default of 1000.
        /// </summary>
        public int MaxItems { get; set; }

        /// <summary>
        /// Gets or sets how the matched items are sorted.
        /// </summary>
        public OrderDto Order { get; set; } = new();
    }
}
