using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.SmarterPlaylist.Api;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using Xunit;

namespace Jellyfin.Plugin.SmarterPlaylist.Tests
{
    public class LibraryValueSourceTest
    {
        // The credits here must be the same ones OperandFactory fills each member from. If they drift,
        // the picker offers names that are in the library but never under that member, which is the
        // exact failure the picker exists to remove.
        [Theory]
        [InlineData("Actors", PersonKind.Actor)]
        [InlineData("Composers", PersonKind.Composer)]
        [InlineData("Directors", PersonKind.Director)]
        [InlineData("GuestStars", PersonKind.GuestStar)]
        [InlineData("Producers", PersonKind.Producer)]
        [InlineData("Writers", PersonKind.Writer)]
        public void PeopleMembersMapToTheCreditThatFillsThem(string member, PersonKind expected)
        {
            Assert.Equal(expected, LibraryValueSource.PersonKindFor(member));
            Assert.True(LibraryValueSource.IsSupported(member));
            Assert.False(LibraryValueSource.IsItemBacked(member));
        }

        [Theory]
        [InlineData("Genres")]
        [InlineData("Studios")]
        [InlineData("Tags")]
        [InlineData("OfficialRating")]
        [InlineData("Album")]
        [InlineData("SeriesName")]
        [InlineData("SeasonName")]
        public void ItemBackedMembersAreSupportedAndAreNotPeople(string member)
        {
            Assert.True(LibraryValueSource.IsSupported(member));
            Assert.True(LibraryValueSource.IsItemBacked(member));
            Assert.Null(LibraryValueSource.PersonKindFor(member));
        }

        // Free text and numbers have no vocabulary to offer. Claiming otherwise would put an empty
        // picker under a field the user must type into anyway.
        [Theory]
        [InlineData("Name")]
        [InlineData("FolderPath")]
        [InlineData("MediaType")]
        [InlineData("CommunityRating")]
        [InlineData("ProductionYear")]
        [InlineData("IsPlayed")]
        [InlineData("PremiereDate")]
        [InlineData("NotAMemberAtAll")]
        public void MembersWithNoLibraryVocabularyAreNotSupported(string member)
        {
            Assert.False(LibraryValueSource.IsSupported(member));
        }

        [Fact]
        public void ListValuesComeBackWholeRatherThanConcatenated()
        {
            var movie = new Movie { Genres = ["Comedy", "Documentary"], Studios = ["Aardman"], Tags = ["rewatch"] };

            Assert.Equal(["Comedy", "Documentary"], LibraryValueSource.ValuesFrom("Genres", movie));
            Assert.Equal(["Aardman"], LibraryValueSource.ValuesFrom("Studios", movie));
            Assert.Equal(["rewatch"], LibraryValueSource.ValuesFrom("Tags", movie));
        }

        [Fact]
        public void SingleValuedMembersYieldOneValue()
        {
            var movie = new Movie { OfficialRating = "PG" };

            Assert.Equal(["PG"], LibraryValueSource.ValuesFrom("OfficialRating", movie));
        }

        // Jellyfin's entities are compiled without nullable annotations, so absent values arrive as
        // null rather than empty. Offering "" as a pickable value would be a rule that matches nothing.
        [Fact]
        public void AbsentValuesAreOmittedRatherThanOfferedAsBlanks()
        {
            var movie = new Movie();

            Assert.Empty(LibraryValueSource.ValuesFrom("OfficialRating", movie));
            Assert.Empty(LibraryValueSource.ValuesFrom("Album", movie));
        }

        // SeriesName lives on Episode, not BaseItem. A movie contributes nothing to it rather than
        // throwing, because one scan covers every member across every supported item type.
        [Fact]
        public void EpisodeOnlyMembersAreEmptyForOtherItemTypes()
        {
            Assert.Empty(LibraryValueSource.ValuesFrom("SeriesName", new Movie { Name = "Arrival" }));
            Assert.Equal(["Star Trek"], LibraryValueSource.ValuesFrom("SeriesName", new Episode { SeriesName = "Star Trek" }));
        }

        [Fact]
        public void AnUnsupportedMemberYieldsNothingRatherThanThrowing()
        {
            Assert.Empty(LibraryValueSource.ValuesFrom("Name", new Movie { Name = "Arrival" }));
        }

        // Reflection over Operand is what makes the schema honest, so the two must agree: every member
        // named here has to still exist on Operand.
        [Fact]
        public void EverySupportedMemberIsARealOperandProperty()
        {
            var operandProperties = typeof(QueryEngine.Operand).GetProperties().Select(p => p.Name).ToHashSet();
            var schema = SchemaBuilder.Build();

            foreach (var member in schema.Members.Where(m => m.LibraryValues))
            {
                Assert.Contains(member.Name, operandProperties);
            }
        }
    }
}
