# Testing Patterns

**Analysis Date:** 2026-07-25

## Test Framework

**Runner:**
- xUnit 2.4.1
- Test host: `Microsoft.NET.Test.Sdk` 17.2.0
- Adapter: `xunit.runner.visualstudio` 2.4.5
- Config: `Jellyfin.Plugin.SmarterPlaylist.Tests/Jellyfin.Plugin.SmarterPlaylist.Tests.csproj` (no `xunit.runner.json`)

**Assertion Library:**
- xUnit's built-in `Assert`. No FluentAssertions or Shouldly.

**Run Commands:**
```bash
dotnet test                                  # Run all tests (mise supplies DOTNET_ROLL_FORWARD)
DOTNET_ROLL_FORWARD=LatestMajor dotnet test  # Explicit form if not using the mise env
dotnet test --filter FullyQualifiedName~EngineTest   # Single class
dotnet test --collect:"XPlat Code Coverage"          # Coverage via coverlet
```

> `DOTNET_ROLL_FORWARD=LatestMajor` is required. The test project targets `net9.0` while `mise.toml` pins the .NET 10 SDK, which ships only the .NET 10 runtime. Without it the run aborts with a missing-framework error rather than a test failure, which is easy to misread as a broken test.

## Test File Organization

**Location:**
- Separate project, mirroring the plugin: `Jellyfin.Plugin.SmarterPlaylist.Tests/`. Tests are not co-located with source.
- The test project references the plugin via `<ProjectReference>` and is marked `<IsPackable>false</IsPackable>`.
- The test project deliberately does **not** enable the analyzer stack, `TreatWarningsAsErrors`, or `GenerateDocumentationFile`, so test code is not required to carry XML docs.

**Naming:**
- `<TypeUnderTest>Test.cs`, with a matching `<TypeUnderTest>Test` class.
- Namespace `Jellyfin.Plugin.SmarterPlaylist.Tests`.
- Test method names are declarative sentences describing the guarantee, not `MethodName_Condition_Result`: `UnknownOrderNameFallsBackToNoOrder`, `MissingCollectionsDeserializeToEmptyRatherThanNull`, `PremiereDateRulesAreRewrittenToUnixSeconds`.

**Structure:**
```
Jellyfin.Plugin.SmarterPlaylist.Tests/
├── Jellyfin.Plugin.SmarterPlaylist.Tests.csproj
├── SmartPlaylistTest.cs        # DTO -> domain mapping (SmarterPlaylist)
├── EngineTest.cs               # Rule compilation and evaluation (QueryEngine.Engine)
└── SmarterPlaylistDtoTest.cs   # JSON binding contract (SmarterPlaylistDto)
```

## Test Structure

**Suite Organization:**
```csharp
public class EngineTest
{
    private static Operand SampleOperand()
    {
        var operand = new Operand("The Hunt for Red October")
        {
            CommunityRating = 7.6f,
            IsPlayed = false,
            MediaType = "Video"
        };

        operand.Directors.Add("John McTiernan");
        operand.Genres.Add("Thriller");

        return operand;
    }

    [Theory]
    [InlineData("Name", "StartsWith", "The Hunt", true)]
    [InlineData("Name", "Equals", "Something Else", false)]
    public void StringMembersSupportStringMethods(string member, string op, string target, bool expected)
    {
        var rule = Engine.CompileRule<Operand>(new Expression(member, op, target));

        Assert.Equal(expected, rule(SampleOperand()));
    }
}
```

**Patterns:**
- Setup: a `private static` factory method returning a fresh fixture per test. No constructor setup, no `IClassFixture`, no shared mutable state.
- Teardown: none needed — nothing touches the filesystem or external resources.
- Assertion: arrange/act/assert separated by blank lines, typically a single assertion or a tight cluster of related ones.
- `[Theory]` with `[InlineData]` is the default for anything with a matrix of inputs; `[Fact]` for one-off guarantees. `EngineTest` leans heavily on `[Theory]` because operator behavior is a function of property type.

## Mocking

**Framework:** None. There is no Moq, NSubstitute, or FakeItEasy dependency.

**Patterns:**
- Tests exercise only the pure, dependency-free core: rule compilation, DTO mapping, JSON binding. These need no test doubles.
```csharp
// No mocks — real objects all the way down
var dto = new SmarterPlaylistDto { Order = new OrderDto { Name = "Not A Real Order" } };

Assert.IsType<NoOrder>(new SmarterPlaylist(dto).Order);
```

**What to Mock:**
- Nothing currently. If the refresh pipeline is ever tested, the Jellyfin manager interfaces (`ILibraryManager`, `IPlaylistManager`, `IUserManager`, `IUserDataManager`, `IProviderManager`) would need doubles, and a mocking library would have to be added.

**What NOT to Mock:**
- `Operand`, `Expression`, `ExpressionSet`, and the DTOs. They are plain data and cheap to construct — building them for real is what makes these tests trustworthy.

## Fixtures and Factories

**Test Data:**
```csharp
// SmarterPlaylistDtoTest pins the exact JSON documented in README.md,
// so the file format stays a tested contract rather than prose.
private const string SampleJson = """
{
  "Name": "CGP Grey",
  "FileName": "cgp_grey",
  "User": "rob",
  "ExpressionSets": [
    {
      "Expressions": [
        { "MemberName": "Directors", "Operator": "Contains", "TargetValue": "CGP Grey" }
      ]
    }
  ],
  "Order": { "Name": "Release Date Ascending" }
}
""";
```

**Location:**
- Inline in the test class as `private const` raw string literals or `private static` factory methods. There is no `Fixtures/` or `TestData/` directory, and no external `.json` files are loaded.

## Coverage

**Requirements:** None enforced. `coverlet.collector` 3.1.2 is referenced but no threshold is configured and CI does not gate on coverage.

**View Coverage:**
```bash
dotnet test --collect:"XPlat Code Coverage"
# Writes TestResults/<guid>/coverage.cobertura.xml
```

**Current state:** 25 tests, all passing. Coverage is concentrated on the pure core and is effectively zero on the I/O and Jellyfin-facing code:

| Area | Covered |
|---|---|
| `QueryEngine/Engine.cs` | Yes — operator matrix, error cases, date rewriting |
| `SmarterPlaylist.cs` mapping | Yes — order selection, MaxItems defaulting |
| `SmarterPlaylistDto.cs` JSON binding | Yes — deserialize, round-trip, missing collections |
| `QueryEngine/OperandFactory.cs` | **No** |
| `SmarterPlaylistStore.cs` / `SmarterPlaylistFileSystem.cs` | **No** |
| `ScheduleTasks/RefreshAllPlaylists.cs` | **No** |
| `Plugin.cs` | **No** (trivial) |

See `CONCERNS.md` for why the refresh-pipeline gap is the one that matters.

## Test Types

**Unit Tests:**
- All 25 tests are unit tests over in-memory objects. No I/O, no async, no external state — the full suite runs in ~13ms.

**Integration Tests:**
- None. Would require either a running Jellyfin server or substantial fakes for its manager interfaces.

**E2E Tests:**
- Not used. Verifying end to end means installing the built DLL into a real Jellyfin instance and running the scheduled task manually.

## Common Patterns

**Async Testing:**
```csharp
// None present — the tested surface is entirely synchronous.
// The async code (SmarterPlaylistStore, RefreshAllPlaylists) is untested.
// When added, follow xUnit's async Task test convention:
[Fact]
public async Task LoadsDefinitionFromDisk()
{
    var result = await store.GetAllSmarterPlaylistsAsync();

    Assert.Single(result);
}
```

**Error Testing:**
```csharp
// Assert.Throws for an exact type
Assert.Throws<MissingMethodException>(() => Engine.CompileRule<Operand>(expression));

// Assert.ThrowsAny when the framework may wrap or subclass the exception
Assert.ThrowsAny<ArgumentException>(() => Engine.CompileRule<Operand>(expression));
```

**Regression guards:** Two tests exist specifically because the behavior they pin was silently broken before. `SmarterPlaylistDtoTest` guards the get-only-collection JSON binding, which deserializes to empty if `[JsonObjectCreationHandling]` is removed. `EngineTest`'s collection cases guard operator resolution over `Collection<string>`. Neither failure mode throws — both produce empty playlists — so these tests are the only thing standing between a refactor and a silent data bug. Do not delete them when touching those types.

---

*Testing analysis: 2026-07-25*
