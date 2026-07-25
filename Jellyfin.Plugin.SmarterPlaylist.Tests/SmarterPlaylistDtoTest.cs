using System.Text.Json;
using Xunit;

namespace Jellyfin.Plugin.SmarterPlaylist.Tests
{
    public class SmarterPlaylistDtoTest
    {
        // The README documents this shape, so deserialization of it is part of the plugin's contract.
        private const string SampleJson = """
        {
          "Name": "CGP Grey",
          "FileName": "cgp_grey",
          "User": "rob",
          "ExpressionSets": [
            {
              "Expressions": [
                { "MemberName": "Directors", "Operator": "Contains", "TargetValue": "CGP Grey" },
                { "MemberName": "IsPlayed", "Operator": "Equal", "TargetValue": "False" }
              ]
            }
          ],
          "Order": { "Name": "Release Date Ascending" }
        }
        """;

        [Fact]
        public void DeserializesDocumentedPlaylistJson()
        {
            var dto = JsonSerializer.Deserialize<SmarterPlaylistDto>(SampleJson);

            Assert.NotNull(dto);
            Assert.Null(dto!.Id);
            Assert.Equal("CGP Grey", dto.Name);
            Assert.Equal("cgp_grey", dto.FileName);
            Assert.Equal("rob", dto.User);
            Assert.Equal("Release Date Ascending", dto.Order.Name);

            var expressions = Assert.Single(dto.ExpressionSets).Expressions;
            Assert.Equal(2, expressions.Count);
            Assert.Equal("Directors", expressions[0].MemberName);
            Assert.Equal("Contains", expressions[0].Operator);
            Assert.Equal("CGP Grey", expressions[0].TargetValue);
        }

        [Fact]
        public void RoundTripsThroughJson()
        {
            var original = JsonSerializer.Deserialize<SmarterPlaylistDto>(SampleJson);
            original!.Id = "87ccaa10f8014a7abe4046ede34adb22";

            var restored = JsonSerializer.Deserialize<SmarterPlaylistDto>(JsonSerializer.Serialize(original));

            Assert.NotNull(restored);
            Assert.Equal(original.Id, restored!.Id);
            Assert.Equal(original.Name, restored.Name);
            Assert.Equal(original.ExpressionSets.Count, restored.ExpressionSets.Count);
            Assert.Equal(
                original.ExpressionSets[0].Expressions.Count,
                restored.ExpressionSets[0].Expressions.Count);
        }

        [Fact]
        public void MissingCollectionsDeserializeToEmptyRatherThanNull()
        {
            var dto = JsonSerializer.Deserialize<SmarterPlaylistDto>("""{ "Name": "Bare" }""");

            Assert.NotNull(dto);
            Assert.Empty(dto!.ExpressionSets);
            Assert.Equal("NoOrder", dto.Order.Name);
        }
    }
}
