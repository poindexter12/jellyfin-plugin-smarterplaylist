using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using Jellyfin.Plugin.SmarterPlaylist.QueryEngine;

namespace Jellyfin.Plugin.SmarterPlaylist
{
    /// <summary>
    /// A group of rules that must all match for an item to be included.
    /// </summary>
    /// <remarks>
    /// Populate handling is required because <see cref="Expressions"/> is get-only; without it
    /// the deserializer silently skips it.
    /// </remarks>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public class ExpressionSet
    {
        /// <summary>
        /// Gets the rules in this set, which are AND'd together.
        /// </summary>
        public Collection<Expression> Expressions { get; } = [];
    }
}
