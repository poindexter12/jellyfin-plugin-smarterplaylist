using System.Linq;
using Jellyfin.Plugin.SmarterPlaylist.Api;
using Jellyfin.Plugin.SmarterPlaylist.QueryEngine;
using Xunit;

namespace Jellyfin.Plugin.SmarterPlaylist.Tests
{
    public class SchemaBuilderTest
    {
        private static MemberDescriptor Member(string name) =>
            Assert.Single(SchemaBuilder.Build().Members.Where(m => m.Name == name));

        [Fact]
        public void DescribesEveryOperandProperty()
        {
            var expected = typeof(Operand).GetProperties().Length;

            Assert.Equal(expected, SchemaBuilder.Build().Members.Count);
        }

        // The whole point of deriving the schema is that adding a filterable property makes it
        // available in the UI without anyone maintaining a second list.
        [Fact]
        public void NoMemberIsUnsupported()
        {
            var unsupported = SchemaBuilder.Build().Members.Where(m => m.Kind == MemberKind.Unsupported).ToList();

            Assert.True(
                unsupported.Count == 0,
                "Unsupported members would render as unusable in the UI: " + string.Join(", ", unsupported.Select(m => m.Name + " (" + m.ClrType + ")")));
        }

        // All five date members must classify as dates, or the UI offers a control whose value the
        // engine will not normalize. The name-suffix heuristic this replaced got four of five wrong.
        [Theory]
        [InlineData("PremiereDate")]
        [InlineData("DateCreated")]
        [InlineData("DateLastRefreshed")]
        [InlineData("DateLastSaved")]
        [InlineData("DateModified")]
        public void DateMembersAreClassifiedAsDates(string name)
        {
            var member = Member(name);

            Assert.Equal(MemberKind.Date, member.Kind);
            Assert.True(member.DateRewritten);
        }

        [Fact]
        public void EveryDateMemberTheEngineNormalizesIsADateInTheSchema()
        {
            var schemaDates = SchemaBuilder.Build().Members
                .Where(m => m.Kind == MemberKind.Date)
                .Select(m => m.Name)
                .OrderBy(n => n, System.StringComparer.Ordinal);

            Assert.Equal(Engine.DateMembers.OrderBy(n => n, System.StringComparer.Ordinal), schemaDates);
        }

        [Theory]
        [InlineData("Name", MemberKind.Text)]
        [InlineData("MediaType", MemberKind.TextEnum)]
        [InlineData("Directors", MemberKind.TextList)]
        [InlineData("CommunityRating", MemberKind.Number)]
        [InlineData("IsPlayed", MemberKind.Boolean)]
        public void ClassifiesMembersByType(string name, MemberKind expected)
        {
            Assert.Equal(expected, Member(name).Kind);
        }

        // The operator lists are what the README documents and what validation enforces; they must
        // agree with what the engine can actually compile.
        [Fact]
        public void CollectionMembersOfferOnlyContainsAndRegex()
        {
            Assert.Equal(["Contains", "MatchRegex", "NotMatchRegex"], Member("Genres").Operators);
        }

        [Fact]
        public void BooleanMembersOfferOnlyEquality()
        {
            Assert.Equal(["Equal", "NotEqual"], Member("IsPlayed").Operators);
        }

        // The two ratings use different scales. Sharing one 0-10 control made every realistic
        // critic-rating rule unenterable.
        [Fact]
        public void RatingMembersCarryTheirOwnRanges()
        {
            var community = Member("CommunityRating");
            var critic = Member("CriticRating");

            Assert.Equal(10, community.Maximum);
            Assert.Equal(0.1, community.Step);
            Assert.Equal(100, critic.Maximum);
            Assert.Equal(1, critic.Step);
            Assert.Contains("percentage", critic.Notes!, System.StringComparison.Ordinal);
        }

        // Ordering or substring operators against a fixed enum value are meaningless, so the UI
        // must not offer them.
        [Fact]
        public void EnumMembersOfferOnlyEqualityOperators()
        {
            Assert.Equal(["Equal", "NotEqual", "Equals"], Member("MediaType").Operators);
        }

        [Fact]
        public void OnlyNumericMembersCarryRangeHints()
        {
            foreach (var m in SchemaBuilder.Build().Members.Where(m => m.Kind != MemberKind.Number))
            {
                Assert.Null(m.Minimum);
                Assert.Null(m.Maximum);
                Assert.Null(m.Step);
            }
        }

        [Fact]
        public void SchemaExposesOrdersAndMediaTypes()
        {
            var schema = SchemaBuilder.Build();

            Assert.Contains("NoOrder", schema.Orders);
            Assert.Contains("Release Date Ascending", schema.Orders);
            Assert.Contains("Audio", schema.MediaTypes);
            Assert.Equal(SmarterPlaylist.DefaultMaxItems, schema.DefaultMaxItems);
        }

        // Every operator the schema advertises must actually compile, or the UI would offer a
        // choice that throws at refresh time -- the failure this page exists to prevent.
        [Fact]
        public void EveryAdvertisedOperatorCompiles()
        {
            foreach (var member in SchemaBuilder.Build().Members.Where(m => m.Kind != MemberKind.Unsupported))
            {
                foreach (var op in member.Operators)
                {
                    var value = member.Kind switch
                    {
                        MemberKind.Boolean => "True",
                        MemberKind.Number => "1",
                        MemberKind.Date => "1593561600",
                        _ => "x"
                    };

                    var ex = Record.Exception(() => Engine.CompileRule<Operand>(new Expression(member.Name, op, value)));
                    Assert.True(ex is null, $"{member.Name}/{op} is advertised but failed to compile: {ex?.Message}");
                }
            }
        }
    }
}
