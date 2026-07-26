namespace Jellyfin.Plugin.SmarterPlaylist.Api
{
    /// <summary>
    /// A request to overwrite an existing definition.
    /// </summary>
    /// <param name="RawJson">The full replacement contents of the file.</param>
    /// <param name="SourceHash">
    /// Hash the client was shown when it loaded the definition. If the file on disk no longer hashes to
    /// this, someone else changed it and the save is refused rather than silently discarding their edit.
    /// </param>
    public sealed record SaveDefinitionRequest(string RawJson, string? SourceHash);
}
