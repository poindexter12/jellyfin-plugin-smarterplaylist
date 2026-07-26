using System.Collections.Generic;

namespace Jellyfin.Plugin.SmarterPlaylist.Api
{
    /// <summary>
    /// Returned when a save is refused because the file changed underneath the editor.
    /// </summary>
    /// <remarks>
    /// Carries the current contents and hash so the client can offer "reload" or "overwrite anyway"
    /// without a second round trip. The client's own hash is stale by definition -- that is why it
    /// received this -- so it cannot construct an overwrite without being told the current one.
    /// </remarks>
    /// <param name="SourceHash">Hash of the file as it now stands on disk.</param>
    /// <param name="RawJson">Current contents of the file, pretty-printed.</param>
    /// <param name="Message">Explanation to show the user.</param>
    public sealed record ConflictResponse(string SourceHash, string RawJson, string Message);
}
