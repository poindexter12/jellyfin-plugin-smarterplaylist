namespace Jellyfin.Plugin.SmarterPlaylist.Api
{
    /// <summary>
    /// A request to evaluate a definition against the library without saving or building a playlist.
    /// </summary>
    /// <param name="RawJson">Definition JSON to evaluate, as currently shown in the editor.</param>
    public sealed record PreviewRequest(string RawJson);
}
