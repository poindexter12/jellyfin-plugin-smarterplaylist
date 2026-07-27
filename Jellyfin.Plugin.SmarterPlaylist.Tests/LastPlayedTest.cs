using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Jellyfin.Plugin.SmarterPlaylist.Api;
using Jellyfin.Plugin.SmarterPlaylist.QueryEngine;
using Xunit;

namespace Jellyfin.Plugin.SmarterPlaylist.Tests
{
    /// <summary>
    /// Filtering on when something was last played, and on windows that move with time.
    /// </summary>
    public class LastPlayedTest
    {
        private static double Unix(DateTime utc) => new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeSeconds();

        private static Operand PlayedAt(DateTime? lastPlayed)
        {
            var operand = new Operand("Episode");
            if (lastPlayed is { } when)
            {
                operand.LastPlayedDate = Unix(when);
            }

            return operand;
        }

        private static double Normalized(string value)
        {
            var set = new ExpressionSet();
            set.Expressions.Add(new Expression(nameof(Operand.LastPlayedDate), "LessThan", value));
            var normalized = Engine.NormalizeRuleSets(new Collection<ExpressionSet> { set });

            return double.Parse(normalized[0].Expressions[0].TargetValue, CultureInfo.InvariantCulture);
        }

        [Fact]
        public void LastPlayedDateIsADateMemberSoReadableDatesWork()
        {
            Assert.Contains(nameof(Operand.LastPlayedDate), Engine.DateMembers);
            Assert.Equal(Unix(new DateTime(2020, 7, 1, 0, 0, 0, DateTimeKind.Utc)), Normalized("2020-07-01"));
        }

        // The whole point of the feature: "not watched recently" has to keep meaning the last N days
        // as days pass, which it only does because rules are normalised on every refresh.
        [Fact]
        public void AnOffsetFromNowResolvesRelativeToTheMomentItIsEvaluated()
        {
            var before = Unix(DateTime.UtcNow.AddDays(-30));
            var resolved = Normalized("now-30d");
            var after = Unix(DateTime.UtcNow.AddDays(-30));

            Assert.InRange(resolved, before - 5, after + 5);
        }

        [Theory]
        [InlineData("now-1h", -1.0 / 24)]
        [InlineData("now-12h", -0.5)]
        [InlineData("now-7d", -7)]
        [InlineData("now-2w", -14)]
        [InlineData("now+1d", 1)]
        public void EveryUnitOffsetsInTheRightDirectionAndScale(string value, double expectedDays)
        {
            var expected = Unix(DateTime.UtcNow.AddDays(expectedDays));

            Assert.InRange(Normalized(value), expected - 60, expected + 60);
        }

        // Calendar arithmetic, not a fixed number of days: "a month ago" is the same day last month.
        [Fact]
        public void MonthsAndYearsUseCalendarArithmetic()
        {
            var month = Unix(DateTime.UtcNow.AddMonths(-1));
            var year = Unix(DateTime.UtcNow.AddYears(-1));

            Assert.InRange(Normalized("now-1m"), month - 60, month + 60);
            Assert.InRange(Normalized("now-1y"), year - 60, year + 60);
        }

        [Fact]
        public void NowOnItsOwnIsTheCurrentInstant()
        {
            var expected = Unix(DateTime.UtcNow);

            Assert.InRange(Normalized("now"), expected - 60, expected + 60);
        }

        [Theory]
        [InlineData("NOW-30D")]
        [InlineData("now - 30 d")]
        public void TheSyntaxIsForgivingAboutCaseAndSpacing(string value)
        {
            var expected = Unix(DateTime.UtcNow.AddDays(-30));

            Assert.InRange(Normalized(value), expected - 60, expected + 60);
        }

        // Rejected rather than guessed at, the same way a bare year is.
        [Theory]
        [InlineData("yesterday")]
        [InlineData("now-30")]
        [InlineData("now-d")]
        [InlineData("-30d")]
        public void SomethingThatIsNotADateIsStillRefused(string value)
        {
            Assert.Throws<ArgumentException>(() => Normalized(value));
        }

        // The M*A*S*H case: everything not watched in the last 30 days, which has to include
        // episodes never watched at all.
        [Fact]
        public void NotPlayedRecentlyIncludesThingsNeverPlayed()
        {
            var rule = Compile("now-30d");

            Assert.True(rule(PlayedAt(null)), "never played should qualify");
            Assert.True(rule(PlayedAt(DateTime.UtcNow.AddDays(-90))), "played long ago should qualify");
            Assert.False(rule(PlayedAt(DateTime.UtcNow.AddDays(-2))), "played recently should not");
        }

        [Fact]
        public void PlayCountIsFilterableAsANumber()
        {
            var members = SchemaBuilder.Build().Members.ToDictionary(m => m.Name, StringComparer.Ordinal);

            Assert.Equal(MemberKind.Number, members[nameof(Operand.PlayCount)].Kind);
            Assert.Equal(MemberKind.Date, members[nameof(Operand.LastPlayedDate)].Kind);
        }

        private static Func<Operand, bool> Compile(string cutoff)
        {
            var set = new ExpressionSet();
            set.Expressions.Add(new Expression(nameof(Operand.LastPlayedDate), "LessThan", cutoff));
            var normalized = Engine.NormalizeRuleSets(new Collection<ExpressionSet> { set });

            return Engine.CompileRule<Operand>(normalized[0].Expressions[0]);
        }
    }
}
