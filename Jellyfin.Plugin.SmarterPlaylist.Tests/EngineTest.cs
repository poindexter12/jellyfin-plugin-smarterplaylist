using System;
using System.Text.Json;
using Jellyfin.Plugin.SmarterPlaylist.QueryEngine;
using Xunit;

namespace Jellyfin.Plugin.SmarterPlaylist.Tests
{
    public class EngineTest
    {
        private static Operand SampleOperand()
        {
            var operand = new Operand("The Hunt for Red October")
            {
                CommunityRating = 7.6f,
                IsPlayed = false,
                MediaType = "Video",
                PremiereDate = 636249600,
                DateCreated = 636249600,
                DateLastRefreshed = 636249600,
                DateLastSaved = 636249600,
                DateModified = 636249600
            };

            operand.Directors.Add("John McTiernan");
            operand.Genres.Add("Thriller");
            operand.Genres.Add("Action");

            return operand;
        }

        [Theory]
        [InlineData("Directors", "Contains", "John McTiernan", true)]
        [InlineData("Directors", "Contains", "CGP Grey", false)]
        [InlineData("Genres", "Contains", "Action", true)]
        public void CollectionMembersSupportContains(string member, string op, string target, bool expected)
        {
            var rule = Engine.CompileRule<Operand>(new Expression(member, op, target));

            Assert.Equal(expected, rule(SampleOperand()));
        }

        // Documented in README: Contains on a list needs a whole exact element, so partial
        // names do not match. MatchRegex is the documented escape hatch for partial matching.
        [Theory]
        [InlineData("Contains", "McTiernan", false)]
        [InlineData("Contains", "John", false)]
        [InlineData("MatchRegex", "McTiernan", true)]
        public void ContainsOnCollectionRequiresAWholeElement(string op, string target, bool expected)
        {
            var rule = Engine.CompileRule<Operand>(new Expression("Directors", op, target));

            Assert.Equal(expected, rule(SampleOperand()));
        }

        // Documented in README: text members accept the binary Equal/NotEqual operators
        // in addition to the string methods.
        [Theory]
        [InlineData("Equal", "The Hunt for Red October", true)]
        [InlineData("Equal", "Something Else", false)]
        [InlineData("NotEqual", "Something Else", true)]
        public void StringMembersSupportBinaryEquality(string op, string target, bool expected)
        {
            var rule = Engine.CompileRule<Operand>(new Expression("Name", op, target));

            Assert.Equal(expected, rule(SampleOperand()));
        }

        // Documented in README: IsPlayed accepts Equal/NotEqual against True/False.
        [Theory]
        [InlineData("NotEqual", "True", true)]
        [InlineData("NotEqual", "False", false)]
        public void BooleanMembersSupportNotEqual(string op, string target, bool expected)
        {
            var rule = Engine.CompileRule<Operand>(new Expression("IsPlayed", op, target));

            Assert.Equal(expected, rule(SampleOperand()));
        }

        [Theory]
        [InlineData("Name", "StartsWith", "The Hunt", true)]
        [InlineData("Name", "EndsWith", "October", true)]
        [InlineData("Name", "Contains", "Red", true)]
        [InlineData("Name", "Equals", "Something Else", false)]
        public void StringMembersSupportStringMethods(string member, string op, string target, bool expected)
        {
            var rule = Engine.CompileRule<Operand>(new Expression(member, op, target));

            Assert.Equal(expected, rule(SampleOperand()));
        }

        [Theory]
        [InlineData("IsPlayed", "Equal", "False", true)]
        [InlineData("IsPlayed", "Equal", "True", false)]
        [InlineData("CommunityRating", "GreaterThan", "7.0", true)]
        [InlineData("CommunityRating", "LessThan", "7.0", false)]
        public void ScalarMembersSupportBinaryOperators(string member, string op, string target, bool expected)
        {
            var rule = Engine.CompileRule<Operand>(new Expression(member, op, target));

            Assert.Equal(expected, rule(SampleOperand()));
        }

        [Theory]
        [InlineData("MatchRegex", "^Video$", true)]
        [InlineData("MatchRegex", "^Audio$", false)]
        [InlineData("NotMatchRegex", "^Audio$", true)]
        public void RegexOperatorsMatchAgainstStringMembers(string op, string pattern, bool expected)
        {
            var rule = Engine.CompileRule<Operand>(new Expression("MediaType", op, pattern));

            Assert.Equal(expected, rule(SampleOperand()));
        }

        // Regex against a collection must match element-wise. Matching the collection's own
        // ToString() tests the CLR type name, so MatchRegex never matched and NotMatchRegex
        // matched everything -- both silently, with no error at any layer.
        [Theory]
        [InlineData("Directors", "MatchRegex", "McTiernan", true)]
        [InlineData("Directors", "MatchRegex", "^John McTiernan$", true)]
        [InlineData("Directors", "MatchRegex", "Spielberg", false)]
        [InlineData("Genres", "MatchRegex", "^(Action|Comedy)$", true)]
        [InlineData("Directors", "NotMatchRegex", "Spielberg", true)]
        [InlineData("Directors", "NotMatchRegex", "McTiernan", false)]
        public void RegexOperatorsMatchCollectionMembersElementWise(string member, string op, string pattern, bool expected)
        {
            var rule = Engine.CompileRule<Operand>(new Expression(member, op, pattern));

            Assert.Equal(expected, rule(SampleOperand()));
        }

        [Fact]
        public void RegexAgainstCollectionDoesNotMatchTheClrTypeName()
        {
            var rule = Engine.CompileRule<Operand>(new Expression("Directors", "MatchRegex", "ObjectModel|Collection"));

            Assert.False(rule(SampleOperand()));
        }

        [Fact]
        public void RegexAgainstEmptyCollectionDoesNotMatch()
        {
            var rule = Engine.CompileRule<Operand>(new Expression("Writers", "MatchRegex", ".*"));

            Assert.False(rule(SampleOperand()));
        }

        [Fact]
        public void UnknownMemberNameThrows()
        {
            var expression = new Expression("NoSuchProperty", "Equal", "x");

            Assert.ThrowsAny<ArgumentException>(() => Engine.CompileRule<Operand>(expression));
        }

        // Was MissingMethodException, back when an operator was resolved by looking for a method of
        // that name on the member's CLR type. There is no method to miss now: the name is either in
        // the registry for this member's kind or it is not a valid operator at all.
        [Fact]
        public void UnknownOperatorThrows()
        {
            var expression = new Expression("Name", "NoSuchOperator", "x");

            var ex = Assert.Throws<ArgumentException>(() => Engine.CompileRule<Operand>(expression));

            Assert.Contains("NoSuchOperator", ex.Message, StringComparison.Ordinal);
            Assert.Contains("StartsWith", ex.Message, StringComparison.Ordinal);
        }

        // The vocabulary is per member kind, so an operator valid somewhere is still rejected on a
        // member it does not apply to. Reflection could not express this: Contains resolved against
        // whatever type the member happened to be, so what was accepted varied by accident.
        [Fact]
        public void OperatorValidForAnotherKindIsRejected()
        {
            var expression = new Expression("ProductionYear", "StartsWith", "19");

            var ex = Assert.Throws<ArgumentException>(() => Engine.CompileRule<Operand>(expression));

            Assert.Contains("ProductionYear", ex.Message, StringComparison.Ordinal);
        }

        // The reason the registry exists. Collection<string>.Remove(string) returns bool, so it
        // type-checked as a predicate and compiled: evaluating the rule removed the value from the
        // item being tested. Nothing advertised it, but a hand-written definition file reached it.
        [Theory]
        [InlineData("Remove")]
        [InlineData("Add")]
        [InlineData("Clear")]
        [InlineData("Insert")]
        public void MutatingMethodsAreNotOperators(string name)
        {
            var expression = new Expression("Genres", name, "Comedy");

            Assert.Throws<ArgumentException>(() => Engine.CompileRule<Operand>(expression));
        }

        // Normalization must not touch the caller's rule set. The rules belong to the deserialized
        // DTO, which is written back to disk on first creation -- so mutating in place rewrote the
        // user's own "2020-07-01" to "1593561600" in their file. Combined with the old code path,
        // which then failed to re-parse that timestamp as a date, a PremiereDate rule worked exactly
        // once and afterwards permanently aborted every playlist's refresh.
        [Fact]
        public void NormalizationDoesNotMutateTheCallersRuleSet()
        {
            var set = new ExpressionSet();
            set.Expressions.Add(new Expression("PremiereDate", "LessThan", "2020-07-01T00:00:00Z"));

            var normalized = Engine.NormalizeRules(set);

            Assert.Equal("2020-07-01T00:00:00Z", set.Expressions[0].TargetValue);
            Assert.Equal("1593561600", normalized.Expressions[0].TargetValue);
        }

        // The full corruption cycle: normalize, persist what the DTO now holds, reload, normalize
        // again. The user's original value must survive the round trip unchanged.
        [Fact]
        public void RepeatedNormalizationIsStableAcrossASaveReloadCycle()
        {
            var dto = new SmarterPlaylistDto();
            var set = new ExpressionSet();
            set.Expressions.Add(new Expression("PremiereDate", "LessThan", "2020-07-01T00:00:00Z"));
            dto.ExpressionSets.Add(set);

            var firstRun = new SmarterPlaylist(dto);
            var persisted = JsonSerializer.Serialize(dto);
            var reloaded = JsonSerializer.Deserialize<SmarterPlaylistDto>(persisted);
            var secondRun = new SmarterPlaylist(reloaded!);

            Assert.Equal("2020-07-01T00:00:00Z", reloaded!.ExpressionSets[0].Expressions[0].TargetValue);
            Assert.Equal(
                firstRun.ExpressionSets[0].Expressions[0].TargetValue,
                secondRun.ExpressionSets[0].Expressions[0].TargetValue);
        }

        // Normalization originally rewrote only PremiereDate, so a human-readable date on any of the
        // other four date members reached Convert.ChangeType as a string and threw FormatException
        // at compile time -- which aborts the whole refresh run, not just that playlist.
        [Theory]
        [InlineData("PremiereDate")]
        [InlineData("DateCreated")]
        [InlineData("DateLastRefreshed")]
        [InlineData("DateLastSaved")]
        [InlineData("DateModified")]
        public void EveryDateMemberAcceptsAHumanReadableDate(string member)
        {
            var set = new ExpressionSet();
            set.Expressions.Add(new Expression(member, "LessThan", "2020-07-01T00:00:00Z"));

            var normalized = Engine.NormalizeRules(set);

            Assert.Equal("1593561600", normalized.Expressions[0].TargetValue);
            Assert.True(Engine.CompileRule<Operand>(normalized.Expressions[0])(SampleOperand()));
        }

        // Definitions written before the fix already store raw Unix seconds; those must survive
        // a second normalization pass untouched rather than being re-parsed as a date.
        [Theory]
        [InlineData("PremiereDate")]
        [InlineData("DateCreated")]
        public void AlreadyNumericDateValuesArePassedThrough(string member)
        {
            var set = new ExpressionSet();
            set.Expressions.Add(new Expression(member, "LessThan", "1593561600"));

            Assert.Equal("1593561600", Engine.NormalizeRules(set).Expressions[0].TargetValue);
        }

        // A bare year does not parse as a date, so without an explicit guard it falls through the
        // numeric passthrough as a raw timestamp: "2020" would mean 1970-01-01T00:33:40Z, and a
        // "released after 2020" rule would match almost the entire library with no error shown.
        [Theory]
        [InlineData("2020")]
        [InlineData("1990")]
        [InlineData("1000")]
        [InlineData("9999")]
        public void BareYearIsRejectedRatherThanReadAsATimestamp(string year)
        {
            var set = new ExpressionSet();
            set.Expressions.Add(new Expression("PremiereDate", "GreaterThan", year));

            var ex = Assert.Throws<ArgumentException>(() => Engine.NormalizeRules(set));

            Assert.Contains(year, ex.Message, StringComparison.Ordinal);
            Assert.Contains("full date", ex.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("1593561600")]
        [InlineData("0")]
        [InlineData("999")]
        public void NumbersOutsideTheBareYearRangeStayTimestamps(string value)
        {
            var set = new ExpressionSet();
            set.Expressions.Add(new Expression("PremiereDate", "GreaterThan", value));

            Assert.Equal(value, Engine.NormalizeRules(set).Expressions[0].TargetValue);
        }

        [Fact]
        public void UnparseableDateValueThrowsWithTheMemberNamed()
        {
            var set = new ExpressionSet();
            set.Expressions.Add(new Expression("DateCreated", "LessThan", "not a date"));

            var ex = Assert.Throws<ArgumentException>(() => Engine.NormalizeRules(set));

            Assert.Contains("DateCreated", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void NonDateRulesAreLeftAlone()
        {
            var set = new ExpressionSet();
            set.Expressions.Add(new Expression("Name", "Contains", "2020-07-01"));

            Assert.Equal("2020-07-01", Engine.NormalizeRules(set).Expressions[0].TargetValue);
        }

        [Fact]
        public void ConvertToUnixTimestampUsesEpochSeconds()
        {
            Assert.Equal(0, Engine.ConvertToUnixTimestamp(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        }
    }
}
