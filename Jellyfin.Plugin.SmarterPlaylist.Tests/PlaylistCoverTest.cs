using System;
using System.Linq;
using Xunit;

namespace Jellyfin.Plugin.SmarterPlaylist.Tests
{
    /// <summary>
    /// The cover key alone decides whether a playlist's artwork is rebuilt, so it has to change
    /// exactly when the cover should and never when it should not.
    /// </summary>
    public class PlaylistCoverTest
    {
        private static Guid[] Ids(int n) =>
            [.. Enumerable.Range(1, n).Select(i => new Guid(i, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]))];

        [Fact]
        public void TheSameItemsInTheSameOrderKeepTheSameCover()
        {
            var dto = new SmarterPlaylistDto();
            var ids = Ids(4);

            Assert.Equal(
                PlaylistCoverService.CoverKey(dto, ids),
                PlaylistCoverService.CoverKey(dto, [.. ids]));
        }

        [Fact]
        public void DifferentItemsMeanADifferentCover()
        {
            var dto = new SmarterPlaylistDto();

            Assert.NotEqual(
                PlaylistCoverService.CoverKey(dto, Ids(4)),
                PlaylistCoverService.CoverKey(dto, Ids(5).Skip(1).ToArray()));
        }

        // The collage lays tiles out in order, so the same four items rearranged really is a
        // different picture.
        [Fact]
        public void ReorderingTheTopItemsMeansADifferentCover()
        {
            var dto = new SmarterPlaylistDto();
            var ids = Ids(4);
            var reversed = ids.Reverse().ToArray();

            Assert.NotEqual(
                PlaylistCoverService.CoverKey(dto, ids),
                PlaylistCoverService.CoverKey(dto, reversed));
        }

        // Only the tiles that appear in the collage matter. Without this, one item leaving the far
        // end of a thousand-item playlist would rebuild artwork that cannot possibly have changed.
        [Fact]
        public void ChangesBelowTheCollageDoNotRebuildIt()
        {
            var dto = new SmarterPlaylistDto();
            var ids = Ids(20);
            var differentTail = ids.Take(4).Concat(Ids(30).Skip(10)).ToArray();

            Assert.Equal(
                PlaylistCoverService.CoverKey(dto, ids),
                PlaylistCoverService.CoverKey(dto, differentTail));
        }

        [Fact]
        public void ANamedImageIgnoresTheItemsEntirely()
        {
            var dto = new SmarterPlaylistDto { Image = "https://example.com/cover.jpg" };

            Assert.Equal(
                PlaylistCoverService.CoverKey(dto, Ids(4)),
                PlaylistCoverService.CoverKey(dto, Ids(9)));
        }

        [Fact]
        public void ChangingTheNamedImageChangesTheCover()
        {
            var ids = Ids(4);

            Assert.NotEqual(
                PlaylistCoverService.CoverKey(new SmarterPlaylistDto { Image = "https://example.com/a.jpg" }, ids),
                PlaylistCoverService.CoverKey(new SmarterPlaylistDto { Image = "https://example.com/b.jpg" }, ids));
        }

        // Switching between a named image and a generated one has to count as a change, or removing
        // the Image field would leave the old picture in place forever.
        [Fact]
        public void AddingOrRemovingANamedImageChangesTheCover()
        {
            var ids = Ids(4);

            Assert.NotEqual(
                PlaylistCoverService.CoverKey(new SmarterPlaylistDto(), ids),
                PlaylistCoverService.CoverKey(new SmarterPlaylistDto { Image = "https://example.com/a.jpg" }, ids));
        }

        [Fact]
        public void AnEmptyPlaylistStillProducesAKey()
        {
            Assert.False(string.IsNullOrEmpty(PlaylistCoverService.CoverKey(new SmarterPlaylistDto(), [])));
        }

        [Fact]
        public void CoverStateIsForgottenOnRequest()
        {
            var store = new PlaylistCoverStore();
            store.Record("doomed", "collage:abc");
            store.Record("kept", "collage:def");

            store.Forget("doomed");

            Assert.Null(store.Get("doomed"));
            Assert.Equal("collage:def", store.Get("kept"));
        }
    }
}
