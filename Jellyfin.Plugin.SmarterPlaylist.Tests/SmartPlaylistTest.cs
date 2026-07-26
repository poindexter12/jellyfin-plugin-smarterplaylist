using Jellyfin.Plugin.SmarterPlaylist.QueryEngine;
using Xunit;

namespace Jellyfin.Plugin.SmarterPlaylist.Tests
{
    public class SmarterPlaylistTest
    {
        [Fact]
        public void DtoToSmarterPlaylist()
        {
            var dto = new SmarterPlaylistDto
            {
                Id = "87ccaa10-f801-4a7a-be40-46ede34adb22",
                Name = "Foo",
                User = "Rob"
            };

            var es = new ExpressionSet();
            es.Expressions.Add(new Expression("Name", "Contains", "biz"));
            dto.ExpressionSets.Add(es);
            dto.Order = new OrderDto { Name = "Release Date Descending" };

            var smarterPlaylist = new SmarterPlaylist(dto);

            Assert.Equal(SmarterPlaylist.DefaultMaxItems, smarterPlaylist.MaxItems);
            Assert.Equal("87ccaa10-f801-4a7a-be40-46ede34adb22", smarterPlaylist.Id);
            Assert.Equal("Foo", smarterPlaylist.Name);
            Assert.Equal("Rob", smarterPlaylist.User);
            Assert.Equal("Name", smarterPlaylist.ExpressionSets[0].Expressions[0].MemberName);
            Assert.Equal("Contains", smarterPlaylist.ExpressionSets[0].Expressions[0].Operator);
            Assert.Equal("biz", smarterPlaylist.ExpressionSets[0].Expressions[0].TargetValue);
            Assert.IsType<PremiereDateOrderDesc>(smarterPlaylist.Order);
        }

        [Fact]
        public void UnknownOrderNameFallsBackToNoOrder()
        {
            var dto = new SmarterPlaylistDto { Order = new OrderDto { Name = "Not A Real Order" } };

            Assert.IsType<NoOrder>(new SmarterPlaylist(dto).Order);
        }

        [Fact]
        public void ExplicitMaxItemsIsPreserved()
        {
            var dto = new SmarterPlaylistDto { MaxItems = 25 };

            Assert.Equal(25, new SmarterPlaylist(dto).MaxItems);
        }

        // MaxItems is applied by FilterPlaylistItems, which needs ILibraryManager/IUserDataManager
        // doubles to exercise. Until those exist, only the mapping above is covered -- the cap
        // itself is verified in CONCERNS.md as a known coverage gap, not asserted here.
    }
}
