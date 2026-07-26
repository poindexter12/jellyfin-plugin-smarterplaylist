using System.Linq;
using Jellyfin.Plugin.SmarterPlaylist.Api;
using Jellyfin.Plugin.SmarterPlaylist.QueryEngine;
using Xunit;

namespace Jellyfin.Plugin.SmarterPlaylist.Tests
{
    /// <summary>
    /// Covers filtering a franchise that spans several shows, such as every Star Trek series.
    /// </summary>
    public class SeriesFilteringTest
    {
        private static Operand Episode(string series, int season, int episode)
        {
            var o = new Operand(series + " S" + season + "E" + episode)
            {
                SeriesName = series,
                SeasonNumber = season,
                EpisodeNumber = episode,
                MediaType = "Video"
            };

            return o;
        }

        // The point of the member: one rule selects a whole franchise, without depending on how the
        // library happens to be foldered.
        [Theory]
        [InlineData("MatchRegex", "^Star Trek", true)]
        [InlineData("MatchRegex", "^CSI", false)]
        [InlineData("Contains", "Star Trek: Deep Space Nine", true)]
        [InlineData("StartsWith", "Star Trek", true)]
        public void SeriesNameSelectsAcrossShows(string op, string value, bool expected)
        {
            var rule = Engine.CompileRule<Operand>(new Expression("SeriesName", op, value));

            Assert.Equal(expected, rule(Episode("Star Trek: Deep Space Nine", 1, 1)));
        }

        [Fact]
        public void SeriesNameIsEmptyRatherThanNullForNonEpisodes()
        {
            var rule = Engine.CompileRule<Operand>(new Expression("SeriesName", "Contains", "Star Trek"));

            Assert.False(rule(new Operand("Some Movie")));
        }

        [Fact]
        public void SeasonAndEpisodeNumbersAreFilterable()
        {
            var pilotsOnly = Engine.CompileRule<Operand>(new Expression("EpisodeNumber", "Equal", "1"));

            Assert.True(pilotsOnly(Episode("Star Trek: Voyager", 3, 1)));
            Assert.False(pilotsOnly(Episode("Star Trek: Voyager", 3, 2)));
        }

        [Fact]
        public void NewMembersAppearInTheSchemaWithUsableOperators()
        {
            var members = SchemaBuilder.Build().Members.ToDictionary(m => m.Name);

            Assert.Equal(MemberKind.Text, members["SeriesName"].Kind);
            Assert.Equal(MemberKind.Number, members["SeasonNumber"].Kind);
            Assert.Equal(MemberKind.Number, members["EpisodeNumber"].Kind);
            Assert.Equal(MemberKind.TextList, members["Tags"].Kind);
            Assert.Contains("MatchRegex", members["SeriesName"].Operators);
            Assert.Contains("GreaterThan", members["SeasonNumber"].Operators);
        }

        [Fact]
        public void SchemaOffersTheSeriesOrder()
        {
            Assert.Contains(SeriesEpisodeOrder.OrderName, SchemaBuilder.Build().Orders);
        }

        [Fact]
        public void TheSeriesOrderIsSelectableFromJson()
        {
            var dto = new SmarterPlaylistDto { Order = new OrderDto { Name = SeriesEpisodeOrder.OrderName } };

            Assert.IsType<SeriesEpisodeOrder>(new SmarterPlaylist(dto).Order);
        }
    }
}
