namespace Jellyfin.Plugin.SmarterPlaylist.Api
{
    /// <summary>
    /// A request to validate definition JSON without writing anything.
    /// </summary>
    /// <param name="RawJson">Definition JSON to check.</param>
    public sealed record ValidateRequest(string RawJson);
}
