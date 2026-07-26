using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Jellyfin.Plugin.SmarterPlaylist.Tests
{
    /// <summary>
    /// Pins the file-name rule the create endpoint enforces. The controller's copy is private, so this
    /// asserts the policy itself: what the allowlist admits, and that admitted names cannot escape a
    /// base directory once joined.
    /// </summary>
    public class SafeFileNameTest
    {
        private static readonly Regex _safeFileName =
            new("^[A-Za-z0-9._-]{1,64}$", RegexOptions.None, TimeSpan.FromSeconds(1));

        [Theory]
        [InlineData("cgp_grey")]
        [InlineData("my-playlist")]
        [InlineData("Playlist.2024")]
        [InlineData("a")]
        public void AcceptsOrdinaryNames(string name)
        {
            Assert.Matches(_safeFileName, name);
        }

        [Theory]
        [InlineData("../escape")]
        [InlineData("sub/dir")]
        [InlineData("sub\\dir")]
        [InlineData("/absolute")]
        [InlineData("")]
        [InlineData("has space")]
        [InlineData("quote\"name")]
        [InlineData("semi;colon")]
        public void RejectsAnythingThatCouldLeaveTheDirectory(string name)
        {
            Assert.DoesNotMatch(_safeFileName, name);
        }

        [Fact]
        public void RejectsNamesLongerThanSixtyFourCharacters()
        {
            Assert.DoesNotMatch(_safeFileName, new string('a', 65));
            Assert.Matches(_safeFileName, new string('a', 64));
        }

        // The containment assertion is the guard that holds even if the allowlist is ever loosened:
        // any accepted name, joined and fully resolved, must still sit directly inside the base.
        [Theory]
        [InlineData("cgp_grey")]
        [InlineData("..")]
        [InlineData("...")]
        public void AnAcceptedNameResolvesInsideTheBaseDirectory(string name)
        {
            var basePath = Path.GetFullPath(Path.Join(Path.GetTempPath(), "sp-base"));
            var resolved = Path.GetFullPath(Path.Join(basePath, name + ".json"));

            Assert.Equal(basePath, Path.GetDirectoryName(resolved));
        }

        // Path.Join keeps the base even for a rooted segment, which is the property the file system
        // layer depends on. Path.Combine would return the rooted segment alone, discarding the base --
        // that behaviour is .NET's rather than ours, so it is described here but not exercised.
        [Fact]
        public void JoinKeepsTheBaseEvenForARootedSegment()
        {
            var basePath = Path.Join(Path.GetTempPath(), "sp-base");
            var rooted = Path.DirectorySeparatorChar + "etc" + Path.DirectorySeparatorChar + "passwd";

            Assert.StartsWith(basePath, Path.Join(basePath, rooted), StringComparison.Ordinal);
        }
    }
}
