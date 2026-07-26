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

        // The per-item cost of evaluating a playlist is two lookups: the item's credits and the
        // user's play state. Both are skipped when no rule reads them, so this set is what decides
        // whether a refresh does one database round trip per item or none.
        [Fact]
        public void ReferencedMembersNamesEveryMemberTheRulesRead()
        {
            var dto = new SmarterPlaylistDto();
            var first = new ExpressionSet();
            first.Expressions.Add(new Expression("Genres", "Contains", "Comedy"));
            first.Expressions.Add(new Expression("IsPlayed", "Equal", "False"));
            var second = new ExpressionSet();
            second.Expressions.Add(new Expression("Directors", "Contains", "CGP Grey"));
            dto.ExpressionSets.Add(first);
            dto.ExpressionSets.Add(second);

            var referenced = new SmarterPlaylist(dto).ReferencedMembers;

            Assert.Equal(3, referenced.Count);
            Assert.Contains("Genres", referenced);
            Assert.Contains("IsPlayed", referenced);
            Assert.Contains("Directors", referenced);
        }

        [Fact]
        public void AMemberNoRuleMentionsIsNotReferenced()
        {
            var dto = new SmarterPlaylistDto();
            var set = new ExpressionSet();
            set.Expressions.Add(new Expression("ProductionYear", "GreaterThan", "1990"));
            dto.ExpressionSets.Add(set);

            var referenced = new SmarterPlaylist(dto).ReferencedMembers;

            Assert.DoesNotContain("Actors", referenced);
            Assert.DoesNotContain("IsPlayed", referenced);
        }

        // Case-sensitively, because that is how the engine resolves a member name. Treating
        // "isplayed" as IsPlayed here would skip the play-state lookup for a rule that will then
        // fail to compile anyway, hiding the real error behind a wrong-looking result.
        [Fact]
        public void ReferencedMembersMatchTheEngineOnCase()
        {
            var dto = new SmarterPlaylistDto();
            var set = new ExpressionSet();
            set.Expressions.Add(new Expression("isplayed", "Equal", "False"));
            dto.ExpressionSets.Add(set);

            Assert.DoesNotContain("IsPlayed", new SmarterPlaylist(dto).ReferencedMembers);
        }

        [Fact]
        public void ADefinitionWithNoRulesReferencesNothing()
        {
            Assert.Empty(new SmarterPlaylist(new SmarterPlaylistDto()).ReferencedMembers);
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
