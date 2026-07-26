using System;
using System.Collections.Generic;
using System.Globalization;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.SmarterPlaylist
{
    /// <summary>
    /// Plugin entry point registered with the Jellyfin server.
    /// </summary>
    public class Plugin : BasePlugin<BasePluginConfiguration>, IHasWebPages
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Plugin"/> class.
        /// </summary>
        /// <param name="applicationPaths">Server paths used to locate plugin configuration.</param>
        /// <param name="xmlSerializer">Serializer used to read and write plugin configuration.</param>
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
        }

        /// <summary>
        /// Gets the running plugin instance, or <c>null</c> before the server has constructed it.
        /// </summary>
        public static Plugin? Instance { get; private set; }

        /// <inheritdoc />
        public override Guid Id => Guid.Parse("3311dfd2-fe3b-4367-a3f0-0dcea5ba07cd");

        /// <inheritdoc />
        public override string Name => "SmarterPlaylist";

        /// <inheritdoc />
        public override string Description =>
            "SmarterPlaylist is a Jellyfin plugin that allows you to create dynamic playlists based on various criteria and conditions.";

        /// <inheritdoc />
        public IEnumerable<PluginPageInfo> GetPages()
        {
            return
            [
                new PluginPageInfo
                {
                    Name = Name,
                    EmbeddedResourcePath = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}.Configuration.configPage.html",
                        GetType().Namespace)
                }
            ];
        }
    }
}
