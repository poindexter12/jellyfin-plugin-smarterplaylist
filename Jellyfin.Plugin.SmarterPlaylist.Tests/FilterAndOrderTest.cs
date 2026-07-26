using System;
using System.Linq;
using Jellyfin.Plugin.SmarterPlaylist.QueryEngine;
using Xunit;

namespace Jellyfin.Plugin.SmarterPlaylist.Tests
{
    /// <summary>
    /// Selection, ordering and capping, exercised end to end without a Jellyfin server.
    /// </summary>
    /// <remarks>
    /// Only possible because filtering takes flattened candidates: while it took Jellyfin entities and
    /// the managers needed to project them, none of this could be reached from a test at all.
    /// </remarks>
    public class FilterAndOrderTest
    {
        private static PlaylistCandidate Movie(string name, DateTime? premiere = null, params string[] genres)
        {
            var operand = new Operand(name);
            foreach (var genre in genres)
            {
                operand.Genres.Add(genre);
            }

            return new PlaylistCandidate(Guid.NewGuid(), premiere, operand);
        }

        private static PlaylistCandidate Episode(string series, int season, int episode, DateTime? premiere = null)
        {
            var operand = new Operand($"{series} S{season}E{episode}")
            {
                SeriesName = series,
                SeasonNumber = season,
                EpisodeNumber = episode
            };

            return new PlaylistCandidate(Guid.NewGuid(), premiere, operand);
        }

        private static SmarterPlaylist Playlist(string order, int maxItems, params Expression[] rules)
        {
            var dto = new SmarterPlaylistDto { Order = new OrderDto { Name = order }, MaxItems = maxItems };
            var set = new ExpressionSet();
            foreach (var rule in rules)
            {
                set.Expressions.Add(rule);
            }

            dto.ExpressionSets.Add(set);

            return new SmarterPlaylist(dto);
        }

        [Fact]
        public void OnlyMatchingCandidatesAreSelected()
        {
            var comedy = Movie("Funny", null, "Comedy");
            var drama = Movie("Sad", null, "Drama");

            var result = Playlist(NoOrder.OrderName, 0, new Expression("Genres", "Contains", "Comedy"))
                .FilterPlaylistItems([comedy, drama]);

            Assert.Equal([comedy.Id], result.Ids);
            Assert.Equal(1, result.MatchedCount);
            Assert.False(result.Truncated);
        }

        // Rule groups are OR'd, rules within a group are AND'd. Getting this backwards would quietly
        // turn a two-group definition into one that matches almost nothing.
        [Fact]
        public void GroupsAreOrredAndRulesWithinAGroupAreAnded()
        {
            var dto = new SmarterPlaylistDto();
            var first = new ExpressionSet();
            first.Expressions.Add(new Expression("Genres", "Contains", "Comedy"));
            first.Expressions.Add(new Expression("Name", "Contains", "Funny"));
            var second = new ExpressionSet();
            second.Expressions.Add(new Expression("Genres", "Contains", "Horror"));
            dto.ExpressionSets.Add(first);
            dto.ExpressionSets.Add(second);

            var bothInGroupOne = Movie("Funny Thing", null, "Comedy");
            var halfOfGroupOne = Movie("Serious Thing", null, "Comedy");
            var groupTwo = Movie("Scary Thing", null, "Horror");

            var result = new SmarterPlaylist(dto).FilterPlaylistItems([bothInGroupOne, halfOfGroupOne, groupTwo]);

            Assert.Equal(2, result.MatchedCount);
            Assert.Contains(bothInGroupOne.Id, result.Ids);
            Assert.Contains(groupTwo.Id, result.Ids);
            Assert.DoesNotContain(halfOfGroupOne.Id, result.Ids);
        }

        [Fact]
        public void ReleaseDateAscendingPutsTheOldestFirst()
        {
            var newer = Movie("Newer", new DateTime(2020, 1, 1), "Comedy");
            var older = Movie("Older", new DateTime(1990, 1, 1), "Comedy");

            var result = Playlist(PremiereDateOrder.OrderName, 0, new Expression("Genres", "Contains", "Comedy"))
                .FilterPlaylistItems([newer, older]);

            Assert.Equal([older.Id, newer.Id], result.Ids);
        }

        [Fact]
        public void ReleaseDateDescendingPutsTheNewestFirst()
        {
            var newer = Movie("Newer", new DateTime(2020, 1, 1), "Comedy");
            var older = Movie("Older", new DateTime(1990, 1, 1), "Comedy");

            var result = Playlist(PremiereDateOrderDesc.OrderName, 0, new Expression("Genres", "Contains", "Comedy"))
                .FilterPlaylistItems([older, newer]);

            Assert.Equal([newer.Id, older.Id], result.Ids);
        }

        // The reason PlaylistCandidate carries a nullable date instead of reading the operand's Unix
        // seconds: there, "no date" is zero, which sorts as 1970 -- after every older film rather than
        // apart from them.
        [Fact]
        public void AnUndatedItemDoesNotSortAsNineteenSeventy()
        {
            var undated = Movie("Undated", null, "Comedy");
            var nineteenFifty = Movie("Fifties", new DateTime(1950, 1, 1), "Comedy");

            var result = Playlist(PremiereDateOrder.OrderName, 0, new Expression("Genres", "Contains", "Comedy"))
                .FilterPlaylistItems([nineteenFifty, undated]);

            Assert.Equal([undated.Id, nineteenFifty.Id], result.Ids);
        }

        [Fact]
        public void SeriesOrderGroupsAShowThenRunsItInBroadcastOrder()
        {
            var voyagerS2 = Episode("Star Trek: Voyager", 2, 1);
            var voyagerS1E2 = Episode("Star Trek: Voyager", 1, 2);
            var voyagerS1E1 = Episode("Star Trek: Voyager", 1, 1);
            var deepSpaceNine = Episode("Star Trek: Deep Space Nine", 1, 1);

            var result = Playlist(SeriesEpisodeOrder.OrderName, 0, new Expression("SeriesName", "MatchRegex", "^Star Trek"))
                .FilterPlaylistItems([voyagerS2, voyagerS1E2, voyagerS1E1, deepSpaceNine]);

            Assert.Equal(
                [deepSpaceNine.Id, voyagerS1E1.Id, voyagerS1E2.Id, voyagerS2.Id],
                result.Ids);
        }

        // Episodes group under their series, everything else under its own name, so a mixed playlist
        // does not scatter a show's episodes across the alphabet.
        [Fact]
        public void SeriesOrderSortsNonEpisodesUnderTheirOwnName()
        {
            var zebra = Movie("Zebra", null, "Doc");
            var alpha = Movie("Alpha", null, "Doc");

            var result = Playlist(SeriesEpisodeOrder.OrderName, 0, new Expression("Genres", "Contains", "Doc"))
                .FilterPlaylistItems([zebra, alpha]);

            Assert.Equal([alpha.Id, zebra.Id], result.Ids);
        }

        [Fact]
        public void MaxItemsCapsAfterSortingSoTheCapKeepsTheFirstN()
        {
            var newest = Movie("C", new DateTime(2020, 1, 1), "Comedy");
            var middle = Movie("B", new DateTime(2010, 1, 1), "Comedy");
            var oldest = Movie("A", new DateTime(2000, 1, 1), "Comedy");

            var result = Playlist(PremiereDateOrder.OrderName, 2, new Expression("Genres", "Contains", "Comedy"))
                .FilterPlaylistItems([newest, middle, oldest]);

            Assert.Equal([oldest.Id, middle.Id], result.Ids);
            Assert.Equal(3, result.MatchedCount);
            Assert.True(result.Truncated);
        }

        [Fact]
        public void MaxItemsOfZeroUsesTheDefaultCap()
        {
            var many = Enumerable.Range(0, SmarterPlaylist.DefaultMaxItems + 10)
                .Select(i => Movie("Item " + i, null, "Comedy"))
                .ToList();

            var result = Playlist(NoOrder.OrderName, 0, new Expression("Genres", "Contains", "Comedy"))
                .FilterPlaylistItems(many);

            Assert.Equal(SmarterPlaylist.DefaultMaxItems, result.Ids.Count);
            Assert.Equal(many.Count, result.MatchedCount);
            Assert.True(result.Truncated);
        }

        [Fact]
        public void NothingMatchingYieldsAnEmptyResultRatherThanThrowing()
        {
            var result = Playlist(NoOrder.OrderName, 0, new Expression("Genres", "Contains", "Nope"))
                .FilterPlaylistItems([Movie("Thing", null, "Comedy")]);

            Assert.Empty(result.Ids);
            Assert.Equal(0, result.MatchedCount);
            Assert.False(result.Truncated);
        }
    }
}
