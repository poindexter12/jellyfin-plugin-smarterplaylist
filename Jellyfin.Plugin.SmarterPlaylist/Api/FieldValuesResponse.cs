using System.Collections.Generic;

namespace Jellyfin.Plugin.SmarterPlaylist.Api
{
    /// <summary>
    /// The values a member actually takes in one user's library.
    /// </summary>
    /// <param name="Member">Member the values belong to.</param>
    /// <param name="Values">Distinct values, sorted, capped at the requested limit.</param>
    /// <param name="Truncated">
    /// Whether the library holds more values than were returned, so the UI can say the list is partial
    /// instead of implying a value that exists is not there.
    /// </param>
    /// <param name="ElapsedMs">How long collecting them took, for the same reason a preview reports it.</param>
    public sealed record FieldValuesResponse(
        string Member,
        IReadOnlyList<string> Values,
        bool Truncated,
        long ElapsedMs);
}
