# Technology Stack

**Analysis Date:** 2026-07-25

## Languages

**Primary:**
- C# 13 (implicit via `net9.0`) — all plugin and test source

**Secondary:**
- YAML — plugin manifest `build.yaml`, GitHub Actions workflows in `.github/workflows/`
- XML — MSBuild projects and the `jellyfin.ruleset` analyzer ruleset
- JSON — user-authored playlist definitions (runtime data), `.github/renovate.json`, `.vscode/*.json`

## Runtime

**Environment:**
- .NET 9.0 (`net9.0`) — both `Jellyfin.Plugin.SmarterPlaylist` and the test project
- Hosted in-process by a Jellyfin server; the plugin is a class library, not a standalone executable

**Package Manager:**
- NuGet
- Lockfile: missing (no `packages.lock.json`; `.github/renovate.json` extends `github>jellyfin/.github//renovate-presets/default` for dependency updates)

**Local toolchain:**
- `mise.toml` pins `dotnet = "10"` and sets `DOTNET_ROLL_FORWARD = "LatestMajor"`
- The roll-forward is required because the projects target `net9.0` while the pinned SDK ships only the .NET 10 runtime; without it `dotnet test` aborts with a missing-framework error

## Frameworks

**Core:**
- `Jellyfin.Controller` 10.11.11 — server-side plugin contracts (`BasePlugin`, `ILibraryManager`, `IPlaylistManager`, `IUserManager`, `IUserDataManager`, `IProviderManager`, `IServerApplicationPaths`)
- `Jellyfin.Model` 10.11.11 — shared models (`IScheduledTask`, `TaskTriggerInfo`, `BaseItemKind`, `PersonKind`)

Both are referenced with `<ExcludeAssets>runtime</ExcludeAssets>` so the server's own assemblies are used at runtime and only `Jellyfin.Plugin.SmarterPlaylist.dll` is shipped.

**Testing:**
- `xunit` 2.4.1 — test framework
- `Microsoft.NET.Test.Sdk` 17.2.0 — test host
- `xunit.runner.visualstudio` 2.4.5 — VSTest adapter
- `coverlet.collector` 3.1.2 — coverage collection

**Build/Dev:**
- MSBuild via the .NET SDK
- `StyleCop.Analyzers` 1.2.0-beta.556 — style enforcement
- `SerilogAnalyzer` 0.15.0 — structured-logging correctness
- `SmartAnalyzers.MultithreadingAnalyzer` 1.1.31 — threading correctness

All three analyzers are `PrivateAssets="All"` so they do not flow to consumers.

## Key Dependencies

**Critical:**
- `Jellyfin.Controller` / `Jellyfin.Model` 10.11.11 — the entire plugin surface. The package version and `build.yaml`'s `targetAbi` must move together; a mismatch means the server refuses to load the plugin.

**Infrastructure:**
- `System.Text.Json` (in-box) — playlist definition serialization in `SmarterPlaylistStore`. The DTOs rely on `[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]` because their collection properties are get-only; without it the deserializer silently skips them.
- `System.Linq.Expressions` (in-box) — `QueryEngine/Engine.cs` compiles playlist rules into expression trees at runtime via `Compile(true)`.

## Configuration

**Environment:**
- No runtime environment variables. The plugin reads everything from disk and from Jellyfin's injected services.
- Playlist definitions live in `<JellyfinDataPath>/SmarterPlaylists/*.json`, created on first use by `SmarterPlaylistFileSystem`.
- `DOTNET_ROLL_FORWARD=LatestMajor` is needed for local test runs only (supplied by `mise.toml`).

**Build:**
- `Jellyfin.Plugin.SmarterPlaylist.sln` — solution
- `Jellyfin.Plugin.SmarterPlaylist/Jellyfin.Plugin.SmarterPlaylist.csproj` — plugin project; enables `TreatWarningsAsErrors`, `GenerateDocumentationFile`, `Nullable`, and `AnalysisMode=AllEnabledByDefault`
- `Jellyfin.Plugin.SmarterPlaylist.Tests/Jellyfin.Plugin.SmarterPlaylist.Tests.csproj` — test project (analyzers and doc generation are not enabled here)
- `jellyfin.ruleset` — shared Jellyfin analyzer ruleset; notably disables SA1600 (elements need not carry StyleCop doc headers) while `GenerateDocumentationFile` still enforces CS1591
- `.editorconfig` — 4-space indent, LF endings, UTF-8, `dotnet_sort_system_directives_first`
- `build.yaml` — Jellyfin plugin manifest (`targetAbi: 10.11.0.0`, `framework: net9.0`, GUID, artifact list)

## Platform Requirements

**Development:**
- .NET SDK capable of building `net9.0` (repo pins .NET 10 via mise, relying on roll-forward)
- Any OS the .NET SDK supports; no platform-specific code

**Production:**
- Jellyfin server 10.11.x (`targetAbi: 10.11.0.0`)
- The built `Jellyfin.Plugin.SmarterPlaylist.dll` is dropped into the server's plugin directory, or installed from a plugin repository built by `.github/workflows/publish.yaml`

---

*Stack analysis: 2026-07-25*
