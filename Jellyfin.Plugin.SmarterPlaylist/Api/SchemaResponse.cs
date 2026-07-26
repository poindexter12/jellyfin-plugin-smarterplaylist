using System.Collections.Generic;

namespace Jellyfin.Plugin.SmarterPlaylist.Api
{
    /// <summary>
    /// The filter vocabulary, derived by reflection so it cannot drift from the engine.
    /// </summary>
    /// <param name="Members">Every filterable member.</param>
    /// <param name="Orders">Valid sort order names.</param>
    /// <param name="MediaTypes">Valid values for the MediaType member.</param>
    /// <param name="DefaultMaxItems">Cap applied when a definition does not set one.</param>
    public sealed record SchemaResponse(
        IReadOnlyList<MemberDescriptor> Members,
        IReadOnlyList<string> Orders,
        IReadOnlyList<string> MediaTypes,
        int DefaultMaxItems);
}
