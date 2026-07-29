using System;
using System.Linq;
using Jellyfin.Plugin.SmarterPlaylist.QueryEngine;
using Jellyfin.Plugin.SmarterPlaylist.QueryEngine.Operators;
using Xunit;

namespace Jellyfin.Plugin.SmarterPlaylist.Tests
{
    /// <summary>
    /// Covers the operators the registry made expressible, and the registry's own invariants.
    /// </summary>
    public class OperatorRegistryTest
    {
        private static Operand SampleOperand()
        {
            var operand = new Operand("The Hunt for Red October")
            {
                CommunityRating = 7.6f,
                ProductionYear = 1990,
                MediaType = "Video",
                OfficialRating = "PG",
                SeriesName = string.Empty,
                PremiereDate = 636249600
            };

            operand.Genres.Add("Thriller");
            operand.Genres.Add("Action");
            operand.Directors.Add("John McTiernan");

            return operand;
        }

        private static bool Eval(string member, string op, string value) =>
            Engine.CompileRule<Operand>(new Expression(member, op, value))(SampleOperand());

        [Theory]
        [InlineData("1980,1999", true)]
        [InlineData("1990,1990", true)]
        [InlineData("1991,1999", false)]
        [InlineData("1980,1989", false)]
        public void BetweenIsInclusiveOnBothBounds(string range, bool expected)
        {
            Assert.Equal(expected, Eval("ProductionYear", "Between", range));
        }

        [Fact]
        public void NotBetweenIsTheComplementOfBetween()
        {
            Assert.True(Eval("ProductionYear", "Between", "1980,1999"));
            Assert.False(Eval("ProductionYear", "NotBetween", "1980,1999"));
        }

        [Fact]
        public void BetweenWorksOnFloatingPointMembers()
        {
            Assert.True(Eval("CommunityRating", "Between", "7,8"));
            Assert.False(Eval("CommunityRating", "Between", "8,9"));
        }

        // Each bound is normalized on its own, so a range of offsets stays a window that moves with
        // time rather than a pair of literals that go stale.
        [Fact]
        public void BetweenNormalizesEachDateBoundIndependently()
        {
            var set = new ExpressionSet();
            set.Expressions.Add(new Expression("PremiereDate", "Between", "now-30d,now"));

            var normalized = Engine.NormalizeRules(set);
            var parts = normalized.Expressions[0].TargetValue.Split(',');

            Assert.Equal(2, parts.Length);

            var low = double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
            var high = double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);

            Assert.True(low < high, $"expected {low} to be before {high}");
            Assert.InRange(high - low, (30 * 86400) - 60, (30 * 86400) + 60);
        }

        [Fact]
        public void BetweenRejectsAValueThatIsNotAPair()
        {
            Assert.Throws<ArgumentException>(() => Engine.CompileRule<Operand>(new Expression("ProductionYear", "Between", "1990")));
        }

        [Theory]
        [InlineData("Thriller,Comedy", true)]
        [InlineData("Comedy,Romance", false)]
        [InlineData("Action", true)]
        public void AnyOfMatchesWhenAListHoldsAnyCandidate(string candidates, bool expected)
        {
            Assert.Equal(expected, Eval("Genres", "AnyOf", candidates));
        }

        [Fact]
        public void NoneOfIsTheComplementOfAnyOf()
        {
            Assert.True(Eval("Genres", "AnyOf", "Thriller,Comedy"));
            Assert.False(Eval("Genres", "NoneOf", "Thriller,Comedy"));
            Assert.True(Eval("Genres", "NoneOf", "Comedy,Romance"));
        }

        [Theory]
        [InlineData("Video,Audio", true)]
        [InlineData("Audio,Book", false)]
        public void AnyOfMatchesTheWholeValueOfATextMember(string candidates, bool expected)
        {
            Assert.Equal(expected, Eval("MediaType", "AnyOf", candidates));
        }

        [Fact]
        public void AnyOfIsCaseSensitive()
        {
            Assert.False(Eval("Genres", "AnyOf", "thriller"));
        }

        // Without an escape a value containing a comma splits into two that match nothing, which
        // reads as a broken filter rather than a value needing to be quoted.
        [Fact]
        public void AnyOfTreatsAnEscapedCommaAsPartOfTheValue()
        {
            Assert.Equal(["Lock, Stock", "Snatch"], RuleValueList.Split("Lock\\, Stock,Snatch"));
        }

        [Fact]
        public void SplitAndJoinRoundTripValuesContainingCommas()
        {
            string[] values = ["Lock, Stock", "Snatch"];

            Assert.Equal(values, RuleValueList.Split(RuleValueList.Join(values)));
        }

        [Theory]
        [InlineData("Genres", "thriller", true)]
        [InlineData("Genres", "THRILLER", true)]
        [InlineData("Genres", "thrill", false)]
        public void ContainsIgnoreCaseMatchesWholeElementsOfAList(string member, string value, bool expected)
        {
            Assert.Equal(expected, Eval(member, "ContainsIgnoreCase", value));
        }

        // Mirrors what Contains means for each kind: substring on text, whole element on a list.
        [Theory]
        [InlineData("red october", true)]
        [InlineData("RED OCTOBER", true)]
        [InlineData("blue october", false)]
        public void ContainsIgnoreCaseMatchesSubstringsOfText(string value, bool expected)
        {
            Assert.Equal(expected, Eval("Name", "ContainsIgnoreCase", value));
        }

        [Fact]
        public void NotContainsIsTheComplementOfContains()
        {
            Assert.True(Eval("Genres", "Contains", "Action"));
            Assert.False(Eval("Genres", "NotContains", "Action"));
            Assert.True(Eval("Genres", "NotContains", "Comedy"));
        }

        [Fact]
        public void IsEmptyFindsBlankTextMembers()
        {
            Assert.True(Eval("SeriesName", "IsEmpty", string.Empty));
            Assert.False(Eval("SeriesName", "IsNotEmpty", string.Empty));

            Assert.False(Eval("OfficialRating", "IsEmpty", string.Empty));
            Assert.True(Eval("OfficialRating", "IsNotEmpty", string.Empty));
        }

        [Fact]
        public void IsEmptyFindsListMembersHoldingNothing()
        {
            Assert.True(Eval("Tags", "IsEmpty", string.Empty));
            Assert.False(Eval("Genres", "IsEmpty", string.Empty));
            Assert.True(Eval("Genres", "IsNotEmpty", string.Empty));
        }

        [Fact]
        public void IsEmptyIgnoresWhateverValueTheRuleCarries()
        {
            Assert.True(Eval("Tags", "IsEmpty", "anything at all"));
        }

        // A date member's value is normally rewritten to Unix seconds, which would reject the empty
        // or leftover value an argument-less operator carries.
        [Fact]
        public void NormalizationLeavesValuelessOperatorsAlone()
        {
            var set = new ExpressionSet();
            set.Expressions.Add(new Expression("SeriesName", "IsEmpty", "leftover"));

            Assert.Equal("leftover", Engine.NormalizeRules(set).Expressions[0].TargetValue);
        }

        [Fact]
        public void EveryOperatorAppliesToAtLeastOneKind()
        {
            foreach (var op in OperatorRegistry.All)
            {
                Assert.NotEmpty(op.AppliesTo);
            }
        }

        // One name may serve several kinds -- Contains is a substring test on text and a whole-element
        // test on a list -- but never two operators for the same kind, or which one runs is arbitrary.
        [Fact]
        public void NoTwoOperatorsClaimTheSameNameAndKind()
        {
            var pairs = OperatorRegistry.All
                .SelectMany(o => o.AppliesTo.Select(k => (o.Name, Kind: k)))
                .ToList();

            Assert.Equal(pairs.Count, pairs.Distinct().Count());
        }

        [Fact]
        public void UnsupportedMembersGetNoOperators()
        {
            Assert.Empty(OperatorRegistry.ForKind(MemberKind.Unsupported));
        }
    }
}
