# Coding Conventions

**Analysis Date:** 2026-07-25

Conventions here are not aspirational — `TreatWarningsAsErrors` plus `AnalysisMode=AllEnabledByDefault` means most of them are compile-enforced. A violation fails the build rather than producing a warning.

## Naming Patterns

**Files:**
- One public type per file, file named exactly for the type (StyleCop SA1402/SA1649, both enforced). `Order`, `NoOrder`, `PremiereDateOrder`, and `PremiereDateOrderDesc` each get their own file; interfaces live beside their implementation as `ISmarterPlaylistStore.cs` / `SmarterPlaylistStore.cs`.
- Folders group by role, not by type: `QueryEngine/` for rule compilation, `ScheduleTasks/` for Jellyfin task implementations.

**Functions:**
- PascalCase methods. Async methods carry the `Async` suffix (`GetAllSmarterPlaylistsAsync`, `RefreshPlaylistAsync`, `CreateNewPlaylistAsync`).
- Note one platform-imposed exception: `IPlaylistManager.CreatePlaylist` is async but unsuffixed, so calls to it read as sync when they are not.

**Variables:**
- camelCase locals and parameters (SA1313 enforced — this was violated by the pre-10.11 code, which had `SmarterPlaylistId` and `SmarterPlaylist` as parameter names).
- Private fields are `_camelCase` with a leading underscore. `jellyfin.ruleset` explicitly disables SA1309 to permit this.
- `var` is used pervasively for locals.

**Types:**
- PascalCase. Interfaces prefixed `I`. DTOs suffixed `Dto` (`SmarterPlaylistDto`, `OrderDto`) to distinguish the on-disk shape from the domain model.
- Constants are PascalCase (`DefaultMaxItems`, `OrderName`, `MatchRegexOperator`).

## Code Style

**Formatting:**
- `.editorconfig` at repo root: 4-space indent, LF line endings, UTF-8, trailing whitespace trimmed, final newline required (SA1518 enforces the newline).
- YAML and `.csproj`/XML drop to 2-space indent.
- Block-scoped namespaces throughout (not file-scoped), with types indented inside.

**Linting:**
- `jellyfin.ruleset`, shared with upstream Jellyfin, referenced via `<CodeAnalysisRuleSet>`.
- Analyzers: `StyleCop.Analyzers`, `SerilogAnalyzer`, `SmartAnalyzers.MultithreadingAnalyzer`, all `PrivateAssets="All"`.
- Notable rules **disabled** by the ruleset: SA1101 (no `this.` prefix required), SA1200 (usings outside the namespace are fine), SA1202/SA1204 (member ordering unenforced), SA1309 (underscore fields allowed), SA1600 (StyleCop does not demand doc headers), SA1633 (no file header required).
- Notable rules **escalated to errors**: CA1305 (must specify `IFormatProvider`), CA1725, CA1727, CA1843, CA2016 (must forward `CancellationToken`), CA2254 (log templates must be static).
- Braces are mandatory even on single statements (SA1503) — no one-line `if (x) DoThing();`.
- Elements must be separated by a blank line (SA1516).

## Import Organization

**Order:**
1. `System.*` first (`dotnet_sort_system_directives_first = true` in `.editorconfig`)
2. `Jellyfin.*` (`Jellyfin.Data.Enums`, `Jellyfin.Database.Implementations.Entities`, `Jellyfin.Plugin.SmarterPlaylist.*`)
3. `MediaBrowser.*` (the Jellyfin server contracts)
4. `Microsoft.*` (`Microsoft.Extensions.Logging`)

Alphabetical within the whole list; SA1210 enforces the ordering.

**Path Aliases:**
- None. C# namespaces only.
- One naming collision to know about: `Jellyfin.Plugin.SmarterPlaylist.QueryEngine.Expression` (the plugin's rule type) shadows `System.Linq.Expressions.Expression`. `Engine.cs` disambiguates by fully qualifying every use as `System.Linq.Expressions.Expression`, which is verbose but deliberate — do not add a `using static` shortcut that reintroduces the ambiguity.

## Error Handling

**Patterns:**
- Guard clauses at method entry using the modern throw helpers: `ArgumentNullException.ThrowIfNull(dto)`.
- Fail loudly on programmer error. Reflection lookups that cannot resolve throw rather than returning null: `?? throw new ArgumentException($"Unknown member '{r.MemberName}'...", nameof(r))` and `?? throw new MissingMethodException(tProp.Name, r.Operator)`.
- Deserialization that yields null throws `InvalidOperationException` naming the offending file path.
- Recoverable, per-item conditions are logged and skipped rather than thrown — a playlist naming a nonexistent user logs an error and `return`s, letting other playlists still refresh.
- Never catch to suppress. The pre-10.11 code used `catch (NullReferenceException)` as flow control to detect a missing user; that was replaced with an explicit `if (user is null)` check. Catching NRE to mean "not found" is the anti-pattern to avoid here.
- Nullable reference types are enabled, so nullability is expressed in signatures (`string? Id`, `Plugin? Instance`) rather than by convention.

## Logging

**Framework:** `Microsoft.Extensions.Logging` via `ILogger<Plugin>` injected by Jellyfin's DI container.

**Patterns:**
- Structured templates with **named** placeholders, never positional or interpolated: `_logger.LogInformation("Playlist ID not set, creating new playlist {Playlist}", dto.Name)`.
- This is enforced, not stylistic: CA2254 is an error in `jellyfin.ruleset`, and `SerilogAnalyzer` also checks template correctness. The pre-10.11 code used `{0}`/`{1}` positional placeholders, which the ruleset now rejects.
- `LogError` for conditions that skip work, `LogInformation` for state changes like playlist creation.
- CA1848 (`LoggerMessage` delegates) is disabled, so plain logger calls are acceptable.

## Comments

**When to Comment:**
- Comments explain *why*, not *what*. Sparse by default — the code carries the mechanics.
- The valuable comments in this codebase record constraints a reader cannot infer: that `Operand` property names are the user-facing rule vocabulary, that a definition's stored id is undashed, that populate-handling is required or JSON binding silently no-ops.
- SA1005 requires a space after `//`.

**XML Documentation:**
- `GenerateDocumentationFile` is on and CS1591 is an error, so **every publicly visible member must carry an XML doc comment**. This is the one convention with no exceptions — the build fails otherwise.
- Note the interaction: `jellyfin.ruleset` disables SA1600, so StyleCop does not demand docs, but the compiler does via CS1591. Disabling SA1600 does not exempt you.
- Interface members are documented on the interface; implementations use `/// <inheritdoc />`. Implementations add a `<remarks>` block alongside `<inheritdoc />` only when the concrete behavior needs qualifying (for example, `SmarterPlaylistFileSystem` documenting that it ignores the `userId` parameter).
- Constructors use the standard "Initializes a new instance of the ... class" phrasing. Primary constructors document their parameters with `<param>` on the type declaration.
- `<exception>` tags are used where a method throws for a caller-correctable reason.

## Function Design

**Size:** Small and single-purpose. The 10.11 refactor split a ~40-line `ExecuteAsync` into `ExecuteAsync` (iterate, honor cancellation) plus `RefreshPlaylistAsync` (handle one playlist), and pulled `FindPlaylists` and `BuildRegexExpr` out as named helpers.

**Parameters:**
- Dependencies arrive via constructor injection, ordered alphabetically by interface name in `RefreshAllPlaylists`.
- Long parameter lists break one-per-line.
- Helpers that need no instance state are `private static` (`LoadPlaylistAsync`, `Fill`, `BuildExpr`, `BuildRegexExpr`).

**Return Values:**
- Prefer the narrowest useful type. Public surfaces return `IEnumerable<T>`; collection properties are `Collection<T>` rather than `List<T>` because CA1002 forbids exposing `List<T>`.
- Async methods return `Task`/`Task<T>` and **must** call `.ConfigureAwait(false)` on every await — CA2007 is enforced. This includes `await using` declarations, which is why disposals are written as `var x = ...; await using (x.ConfigureAwait(false)) { ... }` rather than the terser `await using var x = ...`.

## Module Design

**Exports:**
- Public by default for the plugin's model and engine; `internal` where the type is genuinely plugin-private (`OperandFactory`).
- Static holder classes are marked `static` (CA1052), as with `Engine` and `OperandFactory`.

**Barrel Files:** Not applicable to C#. Namespaces mirror the folder structure exactly: `Jellyfin.Plugin.SmarterPlaylist.QueryEngine` lives in `QueryEngine/`, `Jellyfin.Plugin.SmarterPlaylist.ScheduleTasks` in `ScheduleTasks/`.

**Serialization contract:**
- DTO types bound from user JSON carry `[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]`. This is mandatory whenever a collection property is get-only (which CA2227 forces), because `System.Text.Json` otherwise skips such properties silently. Treat the attribute and the get-only collection as a pair.

---

*Convention analysis: 2026-07-25*
