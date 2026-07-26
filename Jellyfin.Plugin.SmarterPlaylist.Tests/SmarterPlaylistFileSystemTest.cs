using System;
using System.IO;
using MediaBrowser.Controller;
using Xunit;

namespace Jellyfin.Plugin.SmarterPlaylist.Tests
{
    public class SmarterPlaylistFileSystemTest : IDisposable
    {
        private readonly string _dataPath;
        private readonly SmarterPlaylistFileSystem _fileSystem;

        public SmarterPlaylistFileSystemTest()
        {
            _dataPath = Path.Join(Path.GetTempPath(), "smarterplaylist-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dataPath);
            _fileSystem = new SmarterPlaylistFileSystem(new StubServerApplicationPaths(_dataPath));
        }

        [Fact]
        public void CreatesThePlaylistDirectoryOnConstruction()
        {
            Assert.True(Directory.Exists(_fileSystem.BasePath));
            Assert.Equal(Path.Join(_dataPath, "SmarterPlaylists"), _fileSystem.BasePath);
        }

        [Fact]
        public void BuildsPathInsideBasePath()
        {
            var path = _fileSystem.GetSmarterPlaylistPath("any-user", "cgp_grey");

            Assert.Equal(Path.Join(_fileSystem.BasePath, "cgp_grey.json"), path);
        }

        // FileName is user-supplied and becomes the file name, so a traversal sequence would
        // otherwise let a definition be written anywhere the server process can reach.
        [Theory]
        [InlineData("../escaped")]
        [InlineData("../../etc/passwd")]
        [InlineData("sub/dir")]
        [InlineData("")]
        [InlineData("   ")]
        public void RejectsFileNamesThatWouldEscapeBasePath(string playlistId)
        {
            Assert.Throws<ArgumentException>(() => _fileSystem.GetSmarterPlaylistPath("any-user", playlistId));
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            if (Directory.Exists(_dataPath))
            {
                Directory.Delete(_dataPath, true);
            }
        }

        // Only DataPath matters here; every other member exists solely to satisfy the interface.
        // If a Jellyfin upgrade adds a member this stub will fail to compile, which is a useful
        // signal that the plugin's view of IServerApplicationPaths has moved.
        private sealed class StubServerApplicationPaths(string dataPath) : IServerApplicationPaths
        {
            public string DataPath { get; } = dataPath;

            public string ProgramDataPath => DataPath;

            public string WebPath => DataPath;

            public string ProgramSystemPath => DataPath;

            public string ImageCachePath => DataPath;

            public string PluginsPath => DataPath;

            public string PluginConfigurationsPath => DataPath;

            public string LogDirectoryPath => DataPath;

            public string ConfigurationDirectoryPath => DataPath;

            public string SystemConfigurationFilePath => Path.Join(DataPath, "system.xml");

            public string CachePath => DataPath;

            public string TempDirectory => DataPath;

            public string VirtualDataPath => DataPath;

            public string TrickplayPath => DataPath;

            public string BackupPath => DataPath;

            public string RootFolderPath => DataPath;

            public string DefaultUserViewsPath => DataPath;

            public string PeoplePath => DataPath;

            public string GenrePath => DataPath;

            public string MusicGenrePath => DataPath;

            public string StudioPath => DataPath;

            public string YearPath => DataPath;

            public string UserConfigurationDirectoryPath => DataPath;

            public string DefaultInternalMetadataPath => DataPath;

            public string InternalMetadataPath => DataPath;

            public string VirtualInternalMetadataPath => DataPath;

            public string ArtistsPath => DataPath;

            public void MakeSanityCheckOrThrow()
            {
            }

            public void CreateAndCheckMarker(string path, string markerName, bool recursive = false)
            {
            }
        }
    }
}
