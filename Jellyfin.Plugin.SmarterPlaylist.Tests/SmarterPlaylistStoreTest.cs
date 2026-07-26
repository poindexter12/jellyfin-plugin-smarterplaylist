using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Jellyfin.Plugin.SmarterPlaylist.Tests
{
    public class SmarterPlaylistStoreTest : IDisposable
    {
        private readonly string _basePath;
        private readonly SmarterPlaylistStore _store;

        public SmarterPlaylistStoreTest()
        {
            _basePath = Path.Join(Path.GetTempPath(), "smarterplaylist-store-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_basePath);
            _store = new SmarterPlaylistStore(new StubFileSystem(_basePath));
        }

        private string WriteDefinition(string fileNameOnDisk, string fileNameField)
        {
            var path = Path.Join(_basePath, fileNameOnDisk + ".json");
            File.WriteAllText(path, $$"""{"Name":"Test","FileName":"{{fileNameField}}","User":"rob"}""");

            return path;
        }

        [Fact]
        public async Task LoadsEveryDefinitionInTheDirectory()
        {
            WriteDefinition("one", "one");
            WriteDefinition("two", "two");

            var all = await _store.GetAllSmarterPlaylistsAsync();

            Assert.Equal(2, all.Length);
        }

        // The on-disk name is the definition's identity: it is what enumeration returns and what a
        // user names their file. The FileName field is free text and can disagree with it. Without
        // reconciliation, saving a divergent definition writes a SECOND file, so one authored
        // playlist silently becomes two, both enumerated and both refreshing.
        [Fact]
        public async Task FileNameIsTakenFromDiskSoADivergentFieldCannotForkTheDefinition()
        {
            WriteDefinition("authored-name", "different-name");

            var loaded = await _store.GetAllSmarterPlaylistsAsync();
            var dto = Assert.Single(loaded);
            Assert.Equal("authored-name", dto.FileName);

            dto.Id = "abc123";
            await _store.SaveAsync(dto);

            Assert.Single(Directory.GetFiles(_basePath, "*.json"));
            Assert.True(File.Exists(Path.Join(_basePath, "authored-name.json")));
            Assert.False(File.Exists(Path.Join(_basePath, "different-name.json")));
        }

        [Fact]
        public async Task SavedDefinitionRoundTripsFromDisk()
        {
            WriteDefinition("round-trip", "round-trip");

            var dto = Assert.Single(await _store.GetAllSmarterPlaylistsAsync());
            dto.Id = "87ccaa10f8014a7abe4046ede34adb22";
            dto.MaxItems = 42;
            await _store.SaveAsync(dto);

            var reloaded = Assert.Single(await _store.GetAllSmarterPlaylistsAsync());
            Assert.Equal("87ccaa10f8014a7abe4046ede34adb22", reloaded.Id);
            Assert.Equal(42, reloaded.MaxItems);
            Assert.Equal("round-trip", reloaded.FileName);
        }

        [Fact]
        public async Task SavedDefinitionIsReadableRatherThanMinified()
        {
            WriteDefinition("readable", "readable");

            var dto = Assert.Single(await _store.GetAllSmarterPlaylistsAsync());
            dto.Id = "abc123";
            await _store.SaveAsync(dto);

            var written = await File.ReadAllTextAsync(Path.Join(_basePath, "readable.json"));
            Assert.Contains("\n", written, StringComparison.Ordinal);
        }

        [Fact]
        public async Task SaveRejectsADefinitionWithNoId()
        {
            WriteDefinition("no-id", "no-id");

            var dto = Assert.Single(await _store.GetAllSmarterPlaylistsAsync());

            await Assert.ThrowsAsync<ArgumentException>(() => _store.SaveAsync(dto));
        }

        [Fact]
        public async Task MalformedJsonFailsNamingTheFile()
        {
            await File.WriteAllTextAsync(Path.Join(_basePath, "broken.json"), "{ not json");

            var ex = await Assert.ThrowsAnyAsync<Exception>(() => _store.GetAllSmarterPlaylistsAsync());

            Assert.Contains("broken.json", ex.ToString(), StringComparison.Ordinal);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            if (Directory.Exists(_basePath))
            {
                Directory.Delete(_basePath, true);
            }
        }

        private sealed class StubFileSystem(string basePath) : ISmarterPlaylistFileSystem
        {
            public string BasePath { get; } = basePath;

            public string GetSmarterPlaylistFilePath(string smarterPlaylistId)
            {
                return Path.Join(BasePath, smarterPlaylistId + ".json");
            }

            public string[] GetSmarterPlaylistFilePaths(string userId)
            {
                return Directory.GetFiles(BasePath, "*.json");
            }

            public string[] GetAllSmarterPlaylistFilePaths()
            {
                return Directory.GetFiles(BasePath, "*.json");
            }

            public string GetSmarterPlaylistPath(string userId, string playlistId)
            {
                if (string.IsNullOrWhiteSpace(playlistId) || Path.GetFileName(playlistId) != playlistId)
                {
                    throw new ArgumentException($"'{playlistId}' is not a valid playlist file name", nameof(playlistId));
                }

                return Path.Join(BasePath, $"{playlistId}.json");
            }
        }
    }
}
