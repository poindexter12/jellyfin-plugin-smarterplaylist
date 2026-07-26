using System.Linq;
using Jellyfin.Plugin.SmarterPlaylist.Api;
using Jellyfin.Plugin.SmarterPlaylist.QueryEngine;
using Xunit;

namespace Jellyfin.Plugin.SmarterPlaylist.Tests
{
    public class DefinitionValidatorTest
    {
        private static SmarterPlaylistDto Valid(params Expression[] rules)
        {
            var dto = new SmarterPlaylistDto { Name = "Test", User = "rob" };
            var set = new ExpressionSet();

            foreach (var rule in rules.DefaultIfEmpty(new Expression("Name", "Contains", "x")))
            {
                set.Expressions.Add(rule);
            }

            dto.ExpressionSets.Add(set);

            return dto;
        }

        private static Diagnostic[] Check(SmarterPlaylistDto dto) =>
            [.. DefinitionValidator.Validate(dto, SchemaBuilder.Build())];

        private static Diagnostic[] Errors(SmarterPlaylistDto dto) =>
            [.. Check(dto).Where(d => d.Severity == DiagnosticSeverity.Error)];

        [Fact]
        public void AValidDefinitionProducesNoErrors()
        {
            Assert.Empty(Errors(Valid()));
        }

        [Fact]
        public void NameAndUserAreRequired()
        {
            var dto = Valid();
            dto.Name = string.Empty;
            dto.User = string.Empty;

            Assert.Equal(2, Errors(dto).Length);
        }

        // Zero groups matches nothing; an empty group matches everything. Neither throws at refresh
        // time, so without validation both are silent wrong answers.
        [Fact]
        public void NoRuleGroupsIsAnError()
        {
            Assert.Contains(Errors(new SmarterPlaylistDto { Name = "n", User = "u" }), d => d.Code == "E04");
        }

        [Fact]
        public void AnEmptyRuleGroupIsAnError()
        {
            var dto = new SmarterPlaylistDto { Name = "n", User = "u" };
            dto.ExpressionSets.Add(new ExpressionSet());

            Assert.Contains(Errors(dto), d => d.Code == "E05");
        }

        [Fact]
        public void UnknownMemberIsReportedWithItsLocation()
        {
            var error = Assert.Single(Errors(Valid(new Expression("Nope", "Contains", "x"))));

            Assert.Equal("E06", error.Code);
            Assert.Contains("Nope", error.Message, System.StringComparison.Ordinal);
            Assert.Equal("ExpressionSets[0].Expressions[0]", error.Path);
        }

        // Member names are case-sensitive, which is the single easiest mistake to make by hand.
        [Fact]
        public void WrongCaseMemberSuggestsTheCorrectName()
        {
            var error = Assert.Single(Errors(Valid(new Expression("directors", "Contains", "x"))));

            Assert.Contains("Did you mean 'Directors'", error.Message, System.StringComparison.Ordinal);
        }

        [Fact]
        public void OperatorNotValidForTheMemberIsReported()
        {
            var error = Assert.Single(Errors(Valid(new Expression("Genres", "StartsWith", "x"))));

            Assert.Equal("E08", error.Code);
            Assert.Contains("Contains", error.Message, System.StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("2020-07-01")]
        [InlineData("2020-07-01T00:00:00Z")]
        [InlineData("1593561600")]
        public void ValidDateValuesAreAccepted(string value)
        {
            Assert.Empty(Errors(Valid(new Expression("PremiereDate", "LessThan", value))));
        }

        // Matches the engine's guard: a bare year would silently mean 1970.
        [Fact]
        public void BareYearIsRejectedAndExplainsWhatItMeans()
        {
            var error = Assert.Single(Errors(Valid(new Expression("PremiereDate", "GreaterThan", "2020"))));

            Assert.Equal("E10", error.Code);
            Assert.Contains("1970", error.Message, System.StringComparison.Ordinal);
            Assert.Contains("2020-01-01", error.Message, System.StringComparison.Ordinal);
        }

        [Fact]
        public void NonDateValueOnADateMemberIsRejected()
        {
            Assert.Contains(Errors(Valid(new Expression("DateCreated", "LessThan", "soon"))), d => d.Code == "E09");
        }

        [Fact]
        public void NonNumericValueOnANumberMemberIsRejected()
        {
            Assert.Contains(Errors(Valid(new Expression("CommunityRating", "GreaterThan", "high"))), d => d.Code == "E12");
        }

        [Fact]
        public void NonBooleanValueOnABooleanMemberIsRejected()
        {
            Assert.Contains(Errors(Valid(new Expression("IsPlayed", "Equal", "yes"))), d => d.Code == "E11");
        }

        [Fact]
        public void InvalidRegexIsReportedRatherThanThrowingAtRefreshTime()
        {
            Assert.Contains(Errors(Valid(new Expression("Name", "MatchRegex", "[unclosed"))), d => d.Code == "E14");
        }

        [Fact]
        public void UnknownOrderIsAWarningNotAnError()
        {
            var dto = Valid();
            dto.Order = new OrderDto { Name = "Nonsense" };

            Assert.Empty(Errors(dto));
            Assert.Contains(Check(dto), d => d.Code == "W01" && d.Severity == DiagnosticSeverity.Warning);
        }

        // Everything the validator rejects must be something the engine would actually fail on, and
        // everything it accepts must compile. Otherwise the page lies in one direction or the other.
        [Fact]
        public void AcceptedRulesCompileAndRejectedRulesWouldNot()
        {
            var accepted = new Expression("Genres", "Contains", "Action");
            Assert.Empty(Errors(Valid(accepted)));
            Assert.Null(Record.Exception(() => Engine.CompileRule<Operand>(accepted)));

            var rejected = new Expression("Genres", "StartsWith", "Act");
            Assert.NotEmpty(Errors(Valid(rejected)));
            Assert.NotNull(Record.Exception(() => Engine.CompileRule<Operand>(rejected)));
        }
    }
}
