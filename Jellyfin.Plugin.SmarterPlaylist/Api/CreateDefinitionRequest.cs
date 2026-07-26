namespace Jellyfin.Plugin.SmarterPlaylist.Api
{
    /// <summary>
    /// A request to create a new definition file.
    /// </summary>
    /// <param name="FileName">On-disk name to create, without extension. Becomes the definition's identity.</param>
    /// <param name="RawJson">Contents of the new file.</param>
    public sealed record CreateDefinitionRequest(string FileName, string RawJson);
}
