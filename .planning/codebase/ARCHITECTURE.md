<!-- refreshed: 2026-07-25 -->
# Architecture

**Analysis Date:** 2026-07-25

## System Overview

```text
┌─────────────────────────────────────────────────────────────┐
│                    Jellyfin Server Host                      │
│   Discovers the plugin, injects services, runs the task      │
├──────────────────────────────┬──────────────────────────────┤
│         Plugin.cs            │  ScheduleTasks/              │
│   BasePlugin registration    │  RefreshAllPlaylists.cs      │
│                              │  (IScheduledTask entry point)│
└──────────────────────────────┴───────────┬──────────────────┘
                                            │
                    ┌───────────────────────┼───────────────────────┐
                    ▼                       ▼                       ▼
┌──────────────────────────┐ ┌──────────────────────┐ ┌────────────────────────┐
│   Persistence            │ │   Domain             │ │   Rule Engine          │
│ SmarterPlaylistStore.cs  │ │ SmarterPlaylist.cs   │ │ QueryEngine/Engine.cs  │
│ SmarterPlaylistFileSystem│ │ Order.cs + subclasses│ │ QueryEngine/Operand.cs │
│ SmarterPlaylistDto.cs    │ │                      │ │ QueryEngine/           │
│ ExpressionSet.cs         │ │                      │ │   OperandFactory.cs    │
│ OrderDto.cs              │ │                      │ │ QueryEngine/           │
│                          │ │                      │ │   Expression.cs        │
└────────────┬─────────────┘ └──────────────────────┘ └───────────┬────────────┘
             │                                                     │
             ▼                                                     ▼
┌──────────────────────────────────┐          ┌──────────────────────────────────┐
│  <JellyfinDataPath>/             │          │  Jellyfin library + user data    │
│    SmarterPlaylists/*.json       │          │  (via ILibraryManager,           │
│  (user-authored definitions)     │          │   IUserDataManager)              │
└──────────────────────────────────┘          └──────────────────────────────────┘
```

## Component Responsibilities

| Component | Responsibility | File |
|-----------|----------------|------|
| `Plugin` | Registers the plugin with Jellyfin; exposes id, name, description | `Jellyfin.Plugin.SmarterPlaylist/Plugin.cs` |
| `RefreshAllPlaylists` | Scheduled entry point; orchestrates the whole refresh cycle | `Jellyfin.Plugin.SmarterPlaylist/ScheduleTasks/RefreshAllPlaylists.cs` |
| `SmarterPlaylistFileSystem` | Owns the on-disk location of definitions; creates the directory | `Jellyfin.Plugin.SmarterPlaylist/SmarterPlaylistFileSystem.cs` |
| `SmarterPlaylistStore` | Serializes definitions to and from JSON | `Jellyfin.Plugin.SmarterPlaylist/SmarterPlaylistStore.cs` |
| `SmarterPlaylistDto` | On-disk shape of a definition (the user-facing file format) | `Jellyfin.Plugin.SmarterPlaylist/SmarterPlaylistDto.cs` |
| `SmarterPlaylist` | Runtime model; resolves order, normalizes and compiles rules, filters items | `Jellyfin.Plugin.SmarterPlaylist/SmarterPlaylist.cs` |
| `Order` hierarchy | Strategy for sorting matched items | `Order.cs`, `NoOrder.cs`, `PremiereDateOrder.cs`, `PremiereDateOrderDesc.cs` |
| `Engine` | Compiles `Expression` rules into `Func<Operand, bool>` via expression trees | `Jellyfin.Plugin.SmarterPlaylist/QueryEngine/Engine.cs` |
| `Operand` | Flattened, user-filterable projection of a library item | `Jellyfin.Plugin.SmarterPlaylist/QueryEngine/Operand.cs` |
| `OperandFactory` | Builds an `Operand` from a `BaseItem` for a given user | `Jellyfin.Plugin.SmarterPlaylist/QueryEngine/OperandFactory.cs` |
| `Expression` | One rule: member name, operator, target value | `Jellyfin.Plugin.SmarterPlaylist/QueryEngine/Expression.cs` |

## Pattern Overview

**Overall:** Scheduled batch pipeline with a runtime-compiled rule engine. There is no request/response path — the plugin has no HTTP surface.

**Key Characteristics:**
- **Interpreter compiled to delegates.** User-authored rules are not walked per item. `Engine` builds a LINQ expression tree per rule and calls `Compile(true)` once, producing a `Func<Operand, bool>` evaluated per item.
- **Reflection-bound contract.** `Operand`'s property names and CLR types *are* the plugin's public API, resolved by string from JSON with no compile-time link.
- **DTO/domain split.** `SmarterPlaylistDto` is the wire format, `SmarterPlaylist` the behavior-bearing model. Mapping happens in the `SmarterPlaylist` constructor.
- **Strategy for ordering.** Sort is polymorphic over `Order`, selected by name in a switch.
- **Host-owned lifecycle.** Jellyfin constructs everything via DI and decides when the task runs.

## Layers

**Host integration:**
- Purpose: Register with Jellyfin and receive control
- Location: `Plugin.cs`, `ScheduleTasks/RefreshAllPlaylists.cs`
- Contains: `BasePlugin<T>` subclass, `IScheduledTask` + `IConfigurableScheduledTask` implementation
- Depends on: `MediaBrowser.Controller`, `MediaBrowser.Model`
- Used by: The Jellyfin server (auto-discovered by assembly scanning)

**Persistence:**
- Purpose: Load and save playlist definitions
- Location: `SmarterPlaylistStore.cs`, `SmarterPlaylistFileSystem.cs`, `SmarterPlaylistDto.cs`, `ExpressionSet.cs`, `OrderDto.cs`
- Contains: `System.Text.Json` serialization, path resolution, plain DTOs
- Depends on: `IServerApplicationPaths` (for the data directory) and the filesystem
- Used by: `RefreshAllPlaylists`

**Domain:**
- Purpose: Represent a playlist and decide which items belong to it
- Location: `SmarterPlaylist.cs`, `Order.cs` and subclasses
- Contains: DTO-to-model mapping, rule-set compilation, filtering and ordering
- Depends on: `QueryEngine`, `ILibraryManager`, `IUserDataManager`
- Used by: `RefreshAllPlaylists`

**Query engine:**
- Purpose: Turn rule data into executable predicates and project items into a filterable shape
- Location: `QueryEngine/`
- Contains: Expression-tree construction, reflection over `Operand`, `BaseItem` projection
- Depends on: `System.Linq.Expressions`, `System.Text.RegularExpressions`, `ILibraryManager`, `IUserDataManager`
- Used by: `SmarterPlaylist`

Dependencies point strictly downward — the query engine knows nothing about persistence, and persistence knows nothing about the domain.

## Data Flow

### Primary Path: scheduled playlist refresh

1. Jellyfin fires the task on its trigger, default 30-minute interval (`ScheduleTasks/RefreshAllPlaylists.cs`, `GetDefaultTriggers`)
2. `ExecuteAsync` loads every definition from disk and iterates, honoring the `CancellationToken` between playlists (`RefreshAllPlaylists.ExecuteAsync`)
3. Per definition, `RefreshPlaylistAsync` builds a `SmarterPlaylist`, which resolves the `Order` and normalizes date rules through `Engine.FixRuleSets` (`SmarterPlaylist` constructor)
4. The owning user is resolved by name; an unknown user is logged and the playlist skipped (`RefreshAllPlaylists.RefreshPlaylistAsync`)
5. If the definition has no id, or no matching playlist exists, a Jellyfin playlist is created and the returned id is written back to the JSON file (`CreateNewPlaylistAsync`, then `SmarterPlaylistStore.SaveAsync`)
6. Every candidate library item is fetched for that user (`GetAllUserMedia`)
7. Each item is projected to an `Operand` and tested against the compiled rules; sets are OR'd, rules within a set are AND'd (`SmarterPlaylist.FilterPlaylistItems`)
8. Matches are sorted by the selected `Order` and reduced to ids (`SmarterPlaylist.FilterPlaylistItems`)
9. The playlist's existing children are removed, then the new item set is added (`RemoveFromPlaylist`, then `IPlaylistManager.AddItemToPlaylistAsync`)

### Secondary Path: rule compilation

1. `SmarterPlaylist.CompileRuleSets` maps each `ExpressionSet` to a list of predicates (`SmarterPlaylist.cs`)
2. `Engine.CompileRule<Operand>` creates a parameter expression and dispatches on operator kind (`QueryEngine/Engine.cs`)
3. `BuildExpr` resolves the named property by reflection, then picks one of three strategies: a binary comparison when the operator parses as an `ExpressionType`; a regex call for `MatchRegex`/`NotMatchRegex`; otherwise a method call on the property type (`Contains`, `StartsWith`, …)
4. The tree is wrapped in a lambda and compiled to a delegate

**State Management:**
- Playlist definitions are the only durable state, owned entirely by the JSON files on disk.
- `Plugin.Instance` is the single piece of global mutable state, assigned in the constructor.
- Nothing is cached between task runs — every run re-reads, re-parses, and re-compiles.

## Key Abstractions

**`Operand`:**
- Purpose: The vocabulary users write rules against — a flattened, user-scoped view of a library item
- Examples: `QueryEngine/Operand.cs`, populated by `QueryEngine/OperandFactory.cs`
- Pattern: Anti-corruption layer over `BaseItem`. Adding a property here extends the plugin's filter language for free; renaming one breaks every user's playlist files.

**`Expression` / `ExpressionSet`:**
- Purpose: Rule data in disjunctive normal form — OR of ANDs
- Examples: `QueryEngine/Expression.cs`, `ExpressionSet.cs`
- Pattern: Interpreter, with the interpretation compiled ahead of evaluation

**`Order`:**
- Purpose: Pluggable sort strategy
- Examples: `Order.cs`, `NoOrder.cs`, `PremiereDateOrder.cs`, `PremiereDateOrderDesc.cs`
- Pattern: Strategy, selected by name. Each concrete order exposes an `OrderName` constant, so the switch in `SmarterPlaylist` and the JSON value cannot drift apart.

**`ISmarterPlaylistStore` / `ISmarterPlaylistFileSystem`:**
- Purpose: Separate serialization from path resolution
- Examples: `ISmarterPlaylistStore.cs`, `ISmarterPlaylistFileSystem.cs`
- Pattern: Repository. Note the abstractions currently buy little, because `RefreshAllPlaylists` constructs the concrete classes itself rather than receiving them from DI.

## Entry Points

**`Plugin`:**
- Location: `Jellyfin.Plugin.SmarterPlaylist/Plugin.cs`
- Triggers: Jellyfin assembly scanning at server start
- Responsibilities: Supply the plugin GUID (`3311dfd2-fe3b-4367-a3f0-0dcea5ba07cd`, which must match `build.yaml`), name, and description. It does **not** implement `IHasWebPages` — there is no configuration page.

**`RefreshAllPlaylists`:**
- Location: `Jellyfin.Plugin.SmarterPlaylist/ScheduleTasks/RefreshAllPlaylists.cs`
- Triggers: Jellyfin's scheduled-task runner, on interval or manual invocation from the dashboard
- Responsibilities: The only code path that does real work. All plugin behavior hangs off this method.

## Architectural Constraints

- **Threading:** The task body is sequential — one playlist at a time, one item at a time. The only concurrency is `Task.WhenAll` over definition-file reads in `SmarterPlaylistStore`. `SmartAnalyzers.MultithreadingAnalyzer` guards against threading mistakes.
- **Global state:** `Plugin.Instance` (`Plugin.cs`) is the sole static mutable field, and nothing currently reads it.
- **Circular imports:** None. The dependency graph is acyclic and strictly downward.
- **Runtime code generation:** `Engine.CompileRule` calls `Compile(true)`, which emits a dynamic assembly. This rules out full AOT/trimming should Jellyfin ever require it.
- **ABI coupling:** The plugin is compiled against a specific Jellyfin version and declares `targetAbi: 10.11.0.0`. It will not load outside that ABI.
- **Cancellation:** The token is honored between playlists, not within one. A single playlist against a very large library will run to completion once started.

## Anti-Patterns

### Reimplementing a platform API from a misdiagnosis

**What happens:** `RefreshAllPlaylists.RemoveFromPlaylist` hand-rolls playlist item removal, rewriting `playlist.LinkedChildren` and calling `UpdateToRepositoryAsync` directly. A comment justified it: *"Real PlaylistManagers RemoveFromPlaylist needs an entry ID which seems to not work."*
**Why it's wrong:** The diagnosis was incorrect. `IPlaylistManager.RemoveItemFromPlaylistAsync` matches on `ItemId?.ToString("N")` — the undashed id format — and the plugin was passing dashed ids. Jellyfin's implementation is otherwise identical to the hand-rolled copy. The workaround reaches past the public API into entity state, so it can break on any Jellyfin release.
**Do this instead:** Call `_playlistManager.RemoveItemFromPlaylistAsync(playlist.Id.ToString(), ids)` with ids formatted via `ToString("N")`, and delete the private method. This also removes the `IFileSystem` and `IProviderManager` dependencies, since Jellyfin queues the metadata refresh itself.

### Using exceptions as control flow

**What happens:** The pre-10.11 code wrapped a playlist lookup in `catch (NullReferenceException)` to detect a missing user.
**Why it's wrong:** It conflates "user not found" with any NRE anywhere in the block, hiding real bugs.
**Do this instead:** Check explicitly — `if (user is null) { _logger.LogError(...); return; }` (`RefreshAllPlaylists.RefreshPlaylistAsync`). Already corrected.

### Silent no-ops in the user-facing contract

**What happens:** `MaxItems` is parsed, defaulted, and exposed, but `FilterPlaylistItems` never applies it. `MatchRegex` on a collection property silently never matches. Get-only collection DTO properties silently deserialize to empty without `[JsonObjectCreationHandling]`.
**Why it's wrong:** Each produces a wrong-but-plausible result with no error at any layer, so users conclude their rules are wrong rather than that the plugin is broken.
**Do this instead:** When a user-facing knob cannot be honored, fail loudly. Where behavior is load-bearing but invisible (the JSON populate handling), pin it with a test — see `SmarterPlaylistDtoTest`.

## Error Handling

**Strategy:** Fail fast on programmer error, log and skip on per-playlist data error.

**Patterns:**
- Guard clauses with `ArgumentNullException.ThrowIfNull` at public entry points
- Reflection misses throw immediately with the offending name (`ArgumentException`, `MissingMethodException`)
- A definition naming an unknown user logs and continues to the next playlist
- **Gap:** anything else thrown inside `RefreshPlaylistAsync` propagates out of `ExecuteAsync` and aborts the whole run, so one malformed definition stops every other playlist from refreshing

## Cross-Cutting Concerns

**Logging:** `ILogger<Plugin>` injected into the scheduled task; structured named placeholders enforced by CA2254 and `SerilogAnalyzer`. No other component logs.
**Validation:** Effectively none. `SmarterPlaylist` has no validation step, and invalid rules surface only as exceptions at compile time mid-run.
**Authentication:** None. The plugin has no external surface; user identity is resolved from the `User` name in each definition via `IUserManager`.

---

*Architecture analysis: 2026-07-25*
