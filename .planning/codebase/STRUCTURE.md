# Codebase Structure

**Analysis Date:** 2026-07-25

## Directory Layout

```
jellyfin-plugin-smarterplaylist/
├── Jellyfin.Plugin.SmarterPlaylist/          # The plugin assembly
│   ├── QueryEngine/                          # Rule compilation and item projection
│   │   ├── Engine.cs                         # Rules -> compiled predicates
│   │   ├── Expression.cs                     # One rule (member, operator, value)
│   │   ├── Operand.cs                        # Filterable projection of a library item
│   │   └── OperandFactory.cs                 # BaseItem -> Operand
│   ├── ScheduleTasks/                        # Jellyfin scheduled tasks
│   │   └── RefreshAllPlaylists.cs            # The only real entry point
│   ├── Properties/
│   │   └── launchSettings.json               # Local debug profile
│   ├── Plugin.cs                             # Jellyfin plugin registration
│   ├── SmarterPlaylist.cs                    # Runtime playlist model
│   ├── SmarterPlaylistDto.cs                 # On-disk definition shape
│   ├── ExpressionSet.cs                      # AND-group of rules
│   ├── OrderDto.cs                           # On-disk sort selection
│   ├── Order.cs                              # Abstract sort strategy
│   ├── NoOrder.cs                            # Sort: library order
│   ├── PremiereDateOrder.cs                  # Sort: release date ascending
│   ├── PremiereDateOrderDesc.cs              # Sort: release date descending
│   ├── ISmarterPlaylistStore.cs              # Definition load/save contract
│   ├── SmarterPlaylistStore.cs               # JSON implementation
│   ├── ISmarterPlaylistFileSystem.cs         # Definition path contract
│   ├── SmarterPlaylistFileSystem.cs          # Data-directory implementation
│   └── Jellyfin.Plugin.SmarterPlaylist.csproj
├── Jellyfin.Plugin.SmarterPlaylist.Tests/    # xUnit test project
│   ├── SmartPlaylistTest.cs                  # DTO -> domain mapping
│   ├── EngineTest.cs                         # Rule compilation and evaluation
│   ├── SmarterPlaylistDtoTest.cs             # JSON binding contract
│   └── Jellyfin.Plugin.SmarterPlaylist.Tests.csproj
├── .github/
│   ├── workflows/                            # All delegate to jellyfin-meta-plugins
│   └── renovate.json                         # Extends the Jellyfin preset
├── .planning/                                # GSD planning artifacts
│   └── codebase/                             # These documents
├── .vscode/                                  # Editor tasks, launch, extensions
├── build.yaml                                # Jellyfin plugin manifest
├── jellyfin.ruleset                          # Shared analyzer ruleset
├── .editorconfig                             # Formatting rules
├── mise.toml                                 # Toolchain pin + DOTNET_ROLL_FORWARD
├── README.md
├── LICENSE
└── Jellyfin.Plugin.SmarterPlaylist.sln
```

## Directory Purposes

**`Jellyfin.Plugin.SmarterPlaylist/`:**
- Purpose: The shipped assembly. Everything here ends up in `Jellyfin.Plugin.SmarterPlaylist.dll`.
- Contains: Plugin registration, persistence, domain model, rule engine
- Key files: `Plugin.cs` (registration), `ScheduleTasks/RefreshAllPlaylists.cs` (entry point)
- Note: the root of this project is flat. Persistence, domain model, and sort strategies all sit side by side; only `QueryEngine/` and `ScheduleTasks/` are broken out.

**`Jellyfin.Plugin.SmarterPlaylist/QueryEngine/`:**
- Purpose: Turn user-authored rule data into executable predicates
- Contains: Expression-tree construction, reflection over `Operand`, `BaseItem` projection
- Key files: `Engine.cs` (the compiler), `Operand.cs` (the user-facing vocabulary)

**`Jellyfin.Plugin.SmarterPlaylist/ScheduleTasks/`:**
- Purpose: Jellyfin `IScheduledTask` implementations
- Contains: One task today
- Key files: `RefreshAllPlaylists.cs`

**`Jellyfin.Plugin.SmarterPlaylist.Tests/`:**
- Purpose: xUnit tests over the pure core
- Contains: Three test classes, no fixtures directory, no mocks
- Key files: `EngineTest.cs`, `SmarterPlaylistDtoTest.cs`

**`.github/workflows/`:**
- Purpose: CI. Every workflow is a thin caller of a `jellyfin/jellyfin-meta-plugins` reusable workflow at `@master`.
- Key files: `build.yaml`, `test.yaml`, `publish.yaml`, `scan-codeql.yaml`

## Key File Locations

**Entry Points:**
- `Jellyfin.Plugin.SmarterPlaylist/Plugin.cs`: Plugin registration; supplies the GUID Jellyfin identifies the plugin by
- `Jellyfin.Plugin.SmarterPlaylist/ScheduleTasks/RefreshAllPlaylists.cs`: The scheduled task — all real behavior starts here

**Configuration:**
- `build.yaml`: Jellyfin plugin manifest. `guid` must match `Plugin.Id`, and `targetAbi`/`framework` must match the csproj's package versions and TFM.
- `Jellyfin.Plugin.SmarterPlaylist/Jellyfin.Plugin.SmarterPlaylist.csproj`: TFM, Jellyfin package versions, analyzer stack, `TreatWarningsAsErrors`
- `jellyfin.ruleset`: Which analyzer rules are errors, warnings, or off
- `.editorconfig`: Formatting
- `mise.toml`: Pins the .NET SDK and sets `DOTNET_ROLL_FORWARD=LatestMajor`, without which `dotnet test` fails to launch

**Core Logic:**
- `Jellyfin.Plugin.SmarterPlaylist/QueryEngine/Engine.cs`: Rule compilation — the heart of the plugin
- `Jellyfin.Plugin.SmarterPlaylist/QueryEngine/Operand.cs`: The set of filterable properties, i.e. the user-facing filter language
- `Jellyfin.Plugin.SmarterPlaylist/SmarterPlaylist.cs`: Filtering and ordering

**Runtime Data (not in the repo):**
- `<JellyfinDataPath>/SmarterPlaylists/*.json`: User-authored definitions, created on first run by `SmarterPlaylistFileSystem`

**Testing:**
- `Jellyfin.Plugin.SmarterPlaylist.Tests/`: All tests

## Naming Conventions

**Files:**
- One public type per file, named exactly for the type: `PremiereDateOrderDesc.cs` contains `PremiereDateOrderDesc`. Enforced by StyleCop SA1402 and SA1649.
- Interfaces are `I`-prefixed and live beside their implementation: `ISmarterPlaylistStore.cs` / `SmarterPlaylistStore.cs`
- DTOs carry a `Dto` suffix: `SmarterPlaylistDto.cs`, `OrderDto.cs`
- Tests are `<TypeUnderTest>Test.cs`

**Directories:**
- PascalCase, named for a role rather than a layer: `QueryEngine/`, `ScheduleTasks/`
- Namespace mirrors directory exactly: `QueryEngine/Engine.cs` is `Jellyfin.Plugin.SmarterPlaylist.QueryEngine`

**Known inconsistency:** the test file `SmartPlaylistTest.cs` is missing the `er` — it should be `SmarterPlaylistTest.cs` to match the `SmarterPlaylistTest` class it contains. Harmless, but it breaks the otherwise strict file-name-matches-type rule.

## Where to Add New Code

**New filterable property (the most common change):**
- Add the property to `Jellyfin.Plugin.SmarterPlaylist/QueryEngine/Operand.cs`
- Populate it in `Jellyfin.Plugin.SmarterPlaylist/QueryEngine/OperandFactory.cs`
- Tests: add cases to `Jellyfin.Plugin.SmarterPlaylist.Tests/EngineTest.cs`
- Nothing else is required — `Engine` resolves properties by reflection, so the new name is immediately usable in playlist JSON. Document it in `README.md`, since the property name is user-facing contract.

**New sort order:**
- New file at the project root, e.g. `NameOrder.cs`, subclassing `Order` with an `OrderName` constant
- Add one arm to the switch in the `SmarterPlaylist` constructor
- Tests: `Jellyfin.Plugin.SmarterPlaylist.Tests/SmartPlaylistTest.cs`
- Document the new name in `README.md`

**New scheduled task:**
- New file in `Jellyfin.Plugin.SmarterPlaylist/ScheduleTasks/` implementing `IScheduledTask`
- No registration needed — Jellyfin discovers tasks by assembly scanning

**New component/module:**
- Implementation at the project root, or in a new PascalCase folder if it forms a cohesive group of three or more types
- Interface in its own file beside the implementation

**Utilities:**
- No `Utils`/`Helpers` directory exists, and none should be added. Shared helpers live as `private static` methods on the type that needs them (see `OperandFactory.Fill`, `Engine.BuildRegexExpr`).

## Special Directories

**`.planning/`:**
- Purpose: GSD planning artifacts, including this codebase map
- Generated: Yes, by GSD commands
- Committed: Yes

**`.vscode/`:**
- Purpose: Editor tasks, launch profiles, recommended extensions
- Generated: No
- Committed: Yes

**`bin/`, `obj/`:**
- Purpose: Build output. `bin/Debug/net9.0/` holds the shippable `Jellyfin.Plugin.SmarterPlaylist.dll` plus the generated `.xml` documentation file.
- Generated: Yes
- Committed: No (gitignored)

**`.remember/`:**
- Purpose: Session history from the remember tooling
- Generated: Yes
- Committed: No (self-gitignored)

---

*Structure analysis: 2026-07-25*
