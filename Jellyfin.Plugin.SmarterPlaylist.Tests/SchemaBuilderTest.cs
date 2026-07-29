using System;
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
        public void CollectionMembersOfferContainsRegexAndSetOperators()
        {
            Assert.Equal(
                ["Contains", "MatchRegex", "NotMatchRegex", "AnyOf", "NoneOf", "ContainsIgnoreCase", "NotContains", "IsEmpty", "IsNotEmpty"],
                Member("Genres").Operators);
        }

        // Ordering ones stay out: greater-than over a list of names means nothing.
        [Fact]
        public void CollectionMembersOfferNoOrderingOperators()
        {
            Assert.DoesNotContain("GreaterThan", Member("Genres").Operators);
            Assert.DoesNotContain("Between", Member("Genres").Operators);
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

        // Ordering and substring operators against a fixed enum value are meaningless, so the UI
        // must not offer them. Set membership is the exception and the reason this list grew:
        // "Video or Audio" was previously two rule groups.
        [Fact]
        public void EnumMembersOfferEqualityAndSetOperators()
        {
            var operators = Member("MediaType").Operators;

            Assert.Equal(
                ["Equal", "NotEqual", "Equals", "MatchRegex", "NotMatchRegex", "AnyOf", "NoneOf", "IsEmpty", "IsNotEmpty"],
                operators);

            Assert.DoesNotContain("GreaterThan", operators);
            Assert.DoesNotContain("StartsWith", operators);
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
            var schema = SchemaBuilder.Build();
            var arities = schema.Operators.ToDictionary(o => o.Name, o => o.Arity, StringComparer.Ordinal);

            foreach (var member in schema.Members.Where(m => m.Kind != MemberKind.Unsupported))
            {
                foreach (var op in member.Operators)
                {
                    var single = member.Kind switch
                    {
                        MemberKind.Boolean => "True",
                        MemberKind.Number => "1",
                        MemberKind.Date => "1593561600",
                        _ => "x"
                    };

                    // The sample has to match the shape the operator expects, or this test would fail
                    // for every multi-value operator regardless of whether it works.
                    var value = arities[op] switch
                    {
                        "None" => string.Empty,
                        "Pair" => $"{single},{single}",
                        "List" => $"{single},{single}",
                        _ => single
                    };

                    var ex = Record.Exception(() => Engine.CompileRule<Operand>(new Expression(member.Name, op, value)));
                    Assert.True(ex is null, $"{member.Name}/{op} is advertised but failed to compile: {ex?.Message}");
                }
            }
        }

        // The schema must describe every operator it advertises on a member, or the page has no way
        // to know what input to draw for it.
        [Fact]
        public void EveryAdvertisedOperatorIsDescribed()
        {
            var schema = SchemaBuilder.Build();
            var described = schema.Operators.Select(o => o.Name).ToHashSet(StringComparer.Ordinal);

            foreach (var member in schema.Members)
            {
                foreach (var op in member.Operators)
                {
                    Assert.Contains(op, described);
                }
            }
        }

        // The page decides whether to draw a value picker from this flag alone, so a member that can be
        // listed but is not flagged silently loses its picker and goes back to being a blank text box.
        [Fact]
        public void EveryListableMemberIsFlaggedForTheValuePicker()
        {
            var members = SchemaBuilder.Build().Members.ToDictionary(m => m.Name, StringComparer.Ordinal);

            foreach (var name in new[]
                     {
                         "Actors", "Album", "Composers", "Directors", "Genres", "GuestStars",
                         "OfficialRating", "Producers", "SeasonName", "SeriesName", "Studios", "Tags", "Writers"
                     })
            {
                Assert.True(members[name].LibraryValues, $"{name} should offer library values");
            }
        }

        [Theory]
        [InlineData("Name")]
        [InlineData("FolderPath")]
        [InlineData("MediaType")]
        [InlineData("CommunityRating")]
        [InlineData("IsPlayed")]
        [InlineData("PremiereDate")]
        public void MembersWithNothingToListAreNotFlagged(string name)
        {
            var member = Assert.Single(SchemaBuilder.Build().Members.Where(m => m.Name == name));

            Assert.False(member.LibraryValues);
        }
    }
}
