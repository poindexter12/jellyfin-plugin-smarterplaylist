using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.SmarterPlaylist
{
    /// <summary>
    /// Registers the plugin's services with the Jellyfin host container.
    /// </summary>
    /// <remarks>
    /// Before this existed, the scheduled task constructed its own store and file system, so nothing
    /// else could share them and neither could be substituted in a test.
    /// </remarks>
    public class PluginServiceRegistrator : IPluginServiceRegistrator
    {
        /// <inheritdoc />
        public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
        {
            // Singleton: last-refresh status must survive between task runs and be visible to the API.
            serviceCollection.AddSingleton<IRefreshStatusStore, RefreshStatusStore>();
            serviceCollection.AddSingleton<ISmarterPlaylistFileSystem, SmarterPlaylistFileSystem>();
            serviceCollection.AddSingleton<ISmarterPlaylistStore, SmarterPlaylistStore>();
        }
    }
}
