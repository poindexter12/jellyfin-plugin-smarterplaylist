# Codebase Concerns

**Analysis Date:** 2026-07-25

Every item below was verified against the code in this repository. Items marked **CONFIRMED BY TEST** were reproduced with an executable check rather than inferred by reading.

## Tech Debt

**Store API is largely dead code:**
- Issue: Three of the five `ISmarterPlaylistStore` members have no callers anywhere in the repo. Only `GetAllSmarterPlaylistsAsync` and `SaveAsync` are used, both from the scheduled task.
- Files: `Jellyfin.Plugin.SmarterPlaylist/ISmarterPlaylistStore.cs`, `Jellyfin.Plugin.SmarterPlaylist/SmarterPlaylistStore.cs`
- Impact: Unused surface implies capability that does not exist (notably `Delete`, which nothing ever invokes, so definitions are never cleaned up by the plugin).
- Fix approach: Either delete `GetSmarterPlaylistAsync`, `LoadPlaylistsAsync`, and `Delete`, or wire them to a real caller once a management surface exists.

**Per-user partitioning is implied but not implemented:**
- Issue: `GetSmarterPlaylistFilePaths(string userId)` and `GetSmarterPlaylistPath(string userId, string playlistId)` accept a `userId` and ignore it entirely. All definitions live flat in one directory keyed only by file name.
- Files: `Jellyfin.Plugin.SmarterPlaylist/SmarterPlaylistFileSystem.cs`
- Impact: Two users cannot own same-named definitions, and `LoadPlaylistsAsync(userId)` would return every user's playlists rather than that user's. Ownership lives only inside each file's `User` field.
- Fix approach: Either partition into `<BasePath>/<userId>/` subdirectories, or drop the parameters so the API stops lying about its behavior.

**Store is constructed rather than injected:**
- Issue: `RefreshAllPlaylists` news up `SmarterPlaylistStore` and `SmarterPlaylistFileSystem` in its own constructor instead of receiving `ISmarterPlaylistStore` from DI.
- Files: `Jellyfin.Plugin.SmarterPlaylist/ScheduleTasks/RefreshAllPlaylists.cs`
- Impact: The task cannot be unit tested against a fake store, which is why there is currently no test coverage of the refresh flow at all. CA1859 also forces the field to be typed as the concrete class.
- Fix approach: Register `ISmarterPlaylistStore`/`ISmarterPlaylistFileSystem` via an `IPluginServiceRegistrator` and inject them.

**Template leftovers in repo configuration:**
- Issue: `scan-codeql.yaml` passes `repository-name: jellyfin/jellyfin-plugin-template` instead of this repository.
- Files: `.github/workflows/scan-codeql.yaml`
- Impact: CodeQL results are attributed to the wrong repository name.
- Fix approach: Change to `poindexter12/jellyfin-plugin-smarterplaylist`. (The sibling leftovers in `build.yaml` — placeholder overview, description, and changelog — were already corrected.)

**No plugin configuration page:**
- Issue: `Plugin` previously implemented `IHasWebPages` but returned a single `PluginPageInfo` with its `EmbeddedResourcePath` commented out, registering an empty page. The interface has been removed rather than left as a broken stub.
- Files: `Jellyfin.Plugin.SmarterPlaylist/Plugin.cs`
- Impact: All configuration is hand-edited JSON; there is no in-server UI.
- Fix approach: Re-add `IHasWebPages` together with a real embedded HTML resource when a config UI is actually built.

## Known Bugs

**`MaxItems` is a silent no-op — CONFIRMED:**
- Symptoms: A definition setting `MaxItems` has no effect; playlists are never truncated.
- Files: `Jellyfin.Plugin.SmarterPlaylist/SmarterPlaylist.cs`, `Jellyfin.Plugin.SmarterPlaylist/SmarterPlaylistDto.cs`
- Trigger: Set `"MaxItems": 10` on any definition with more than 10 matches. All matches are added.
- Detail: The value is parsed, defaulted to `DefaultMaxItems` (1000), and exposed as a property, but `FilterPlaylistItems` never applies a `Take`. The field is also absent from the README's documented field list, so it is both non-functional and undocumented.
- Workaround: None. Narrow the rules instead.

**`MatchRegex`/`NotMatchRegex` are broken on collection properties — CONFIRMED BY TEST:**
- Symptoms: `MatchRegex` against `Directors`, `Genres`, `Actors`, `Composers`, `GuestStars`, `Producers`, or `Studios` never matches. `NotMatchRegex` against those same properties matches every item.
- Files: `Jellyfin.Plugin.SmarterPlaylist/QueryEngine/Engine.cs` (`BuildRegexExpr`)
- Trigger: A rule of `{"MemberName": "Directors", "Operator": "MatchRegex", "TargetValue": "McTiernan"}` against an item directed by John McTiernan returns `false`.
- Cause: `BuildRegexExpr` resolves `ToString()` on the property type. For a collection that binds to `Object.ToString()`, so the pattern is tested against the CLR type name (`System.Collections.ObjectModel.Collection\`1[System.String]`) rather than the collection's contents.
- Severity: `NotMatchRegex` is the dangerous half — it silently returns `true` for everything, so a rule intended to exclude items excludes nothing.
- Fix approach: Detect `IEnumerable<string>` properties and emit an `Any(s => regex.IsMatch(s))` call instead of `regex.IsMatch(prop.ToString())`. Pre-dates the 10.11 migration; behaved identically when these were `List<string>`.
- Workaround: Use `Contains` for collections; reserve regex for the plain string properties (`Name`, `MediaType`, `Album`, `FolderPath`).

**Hand-authored JSON is reflowed to minified JSON:**
- Symptoms: The first refresh after creating a definition rewrites the user's formatted file as a single minified line.
- Files: `Jellyfin.Plugin.SmarterPlaylist/SmarterPlaylistStore.cs` (`SaveAsync`)
- Trigger: Create a definition without an `Id`; the plugin stamps the generated id and serializes with default options.
- Fix approach: Pass `new JsonSerializerOptions { WriteIndented = true }`. Listed as "Pretty Print JSON files" in the README's future work.

## Security Considerations

**User-supplied regular expressions:**
- Risk: Playlist definitions are user-authored and their `TargetValue` is compiled into a `Regex`. A catastrophically backtracking pattern could stall the scheduled task.
- Files: `Jellyfin.Plugin.SmarterPlaylist/QueryEngine/Engine.cs`
- Current mitigation: A 5-second `Regex` match timeout is applied at construction.
- Recommendations: Consider surfacing `RegexMatchTimeoutException` as a per-playlist error rather than letting it fail the whole task run.

**Runtime expression compilation:**
- Risk: `Engine.CompileRule` calls `Compile(true)`, emitting IL from user-controlled member names and operator names.
- Files: `Jellyfin.Plugin.SmarterPlaylist/QueryEngine/Engine.cs`
- Current mitigation: Member and operator names are resolved by reflection against `Operand` only, so an invalid name throws (`ArgumentException` / `MissingMethodException`) rather than executing arbitrary code. The surface is bounded by `Operand`'s properties.
- Recommendations: Acceptable as-is. Note that `Compile(true)` forces a dynamic assembly, which is unavailable under full AOT should Jellyfin ever move that way.

**Definitions are read from a server-writable directory:**
- Risk: Anyone who can write to `<DataPath>/SmarterPlaylists/` controls which user each playlist targets, since ownership is just the `User` string in the file.
- Files: `Jellyfin.Plugin.SmarterPlaylist/SmarterPlaylistFileSystem.cs`
- Current mitigation: Filesystem permissions on the Jellyfin data directory.
- Recommendations: Same trust boundary as the rest of the server's data directory; no additional control needed unless a web-based editor is added, at which point authorization becomes essential.

## Performance Bottlenecks

**Full library scan per playlist, per run:**
- Problem: Every refresh enumerates the entire user-visible library once per playlist definition.
- Files: `Jellyfin.Plugin.SmarterPlaylist/ScheduleTasks/RefreshAllPlaylists.cs` (`GetAllUserMedia`, called inside the per-playlist loop)
- Cause: `GetAllUserMedia` issues an unfiltered recursive `InternalItemsQuery` for `Audio`, `Episode`, and `Movie`, and is invoked once per definition rather than once per user.
- Improvement path: Group definitions by user and fetch the item set once per user per run.

**Operand construction hits the library per item:**
- Problem: `OperandFactory.GetMediaType` calls `ILibraryManager.GetPeople(baseItem)` for every candidate item, for every playlist.
- Files: `Jellyfin.Plugin.SmarterPlaylist/QueryEngine/OperandFactory.cs`
- Cause: People are resolved eagerly even when no rule in the definition references a person property.
- Improvement path: Inspect the compiled rule set for person-property usage and skip the lookup when unused, or memoize per item across definitions in a single run.

**Rules recompiled every run:**
- Problem: Expression trees are rebuilt and JIT-compiled on each execution.
- Files: `Jellyfin.Plugin.SmarterPlaylist/SmarterPlaylist.cs` (`CompileRuleSets`)
- Cause: A fresh `SmarterPlaylist` is constructed per definition per run.
- Improvement path: Cache compiled delegates keyed by definition content hash. Low priority — compilation cost is small next to the library scan.

## Fragile Areas

**Reflection-driven rule engine:**
- Files: `Jellyfin.Plugin.SmarterPlaylist/QueryEngine/Engine.cs`, `Jellyfin.Plugin.SmarterPlaylist/QueryEngine/Operand.cs`
- Why fragile: `Operand`'s property names and CLR types are the plugin's public contract, bound by string from user JSON with no compile-time link. Renaming a property, or changing its type, silently breaks every playlist file using it. Which operators are legal is an emergent consequence of the property's type rather than anything declared.
- Safe modification: Add properties freely; never rename or retype an existing one without a migration. `EngineTest.cs` pins the operator-per-type behavior — run it after any change here.
- Test coverage: Good since the 10.11 migration (operator/type matrix, unknown member, unknown operator, date rewriting).

**DTO/JSON binding contract:**
- Files: `Jellyfin.Plugin.SmarterPlaylist/SmarterPlaylistDto.cs`, `Jellyfin.Plugin.SmarterPlaylist/ExpressionSet.cs`
- Why fragile: Collection properties are get-only to satisfy CA2227, which means `System.Text.Json` will silently skip them unless `[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]` is present on the type. Removing that attribute produces empty playlists with no error at any layer.
- Safe modification: Keep the attribute whenever a collection property is get-only. `SmarterPlaylistDtoTest.cs` loads the exact JSON documented in the README and will fail if this regresses.
- Test coverage: Good — deserialization, round-trip, and missing-collection defaults are all covered.

**Playlist entry removal reimplements a working platform API — VERIFIED, REMOVABLE:**
- Files: `Jellyfin.Plugin.SmarterPlaylist/ScheduleTasks/RefreshAllPlaylists.cs` (`RemoveFromPlaylist`)
- Why fragile: The private `RemoveFromPlaylist` reaches past the public API, rewriting `playlist.LinkedChildren` directly and calling `UpdateToRepositoryAsync` itself. A source comment justified this: *"Real PlaylistManagers RemoveFromPlaylist needs an entry ID which seems to not work."*
- Root cause (verified against Jellyfin v10.11.11 `Emby.Server.Implementations/Playlists/PlaylistManager.cs`): that diagnosis was wrong. `IPlaylistManager.RemoveItemFromPlaylistAsync(string playlistId, IEnumerable<string> entryIds)` matches on `i.Item1.ItemId?.ToString("N", CultureInfo.InvariantCulture)` — the **undashed** id format. The plugin was passing dashed (`"D"`) ids, so nothing ever matched, and the author concluded the API was broken rather than that the format was wrong.
- Detail: Jellyfin's implementation is otherwise line-for-line what the plugin hand-rolled — same `GetManageableItems`, same `LinkedChildren` rewrite, same `QueueRefresh` with `ForceSave`.
- Fix approach: Delete `RemoveFromPlaylist` entirely and call `_playlistManager.RemoveItemFromPlaylistAsync(playlist.Id.ToString(), ids)` passing ids formatted with `ToString("N")`. This also drops the `IFileSystem` and `IProviderManager` constructor dependencies, since Jellyfin queues the metadata refresh internally.
- Test coverage: **None.** The whole refresh path is untested, which is why this went unnoticed.

**Playlist id format is load-bearing and undocumented:**
- Files: `Jellyfin.Plugin.SmarterPlaylist/ScheduleTasks/RefreshAllPlaylists.cs` (`FindPlaylists`, `CreateNewPlaylistAsync`)
- Why fragile: `FindPlaylists` compares `playlist.Id.ToString()` with dashes stripped against the definition's stored `Id`. This is **correct** — Jellyfin's `CreatePlaylist` returns `playlist.Id.ToString("N")`, so stored ids are undashed — but nothing in the code states that invariant. A future edit that stores a dashed id, or drops the `Replace`, would silently stop matching and make the plugin create a duplicate playlist on every run.
- Safe modification: Parse both sides as `Guid` and compare, which is format-agnostic and self-documenting.
- Test coverage: None.

## Scaling Limits

**Library size:**
- Current capacity: Fine for typical personal libraries.
- Limit: Cost is O(definitions x library items) per run against a default 30-minute trigger. A large library combined with many definitions will keep the task running continuously.
- Scaling path: Batch the library fetch per user (see Performance), then consider incremental refresh driven by library-change events instead of a fixed interval.

**Playlist definition count:**
- Current capacity: All definitions are loaded and deserialized concurrently on every run via `Task.WhenAll`.
- Limit: Unbounded concurrency over file handles; fine at tens of files, wasteful at thousands.
- Scaling path: Bound the parallelism if definition counts ever grow.

## Dependencies at Risk

**Jellyfin ABI coupling:**
- Risk: `Jellyfin.Controller`/`Jellyfin.Model` are pinned to 10.11.11 and `build.yaml` declares `targetAbi: 10.11.0.0`. Jellyfin makes breaking changes across minor versions — the 10.9 to 10.11 migration alone moved `User` from `Jellyfin.Data.Entities` to `Jellyfin.Database.Implementations.Entities`, turned `PersonInfo.Type` from `string` into the `PersonKind` enum, replaced `TaskTriggerInfo.TriggerInterval` with `TaskTriggerInfoType.IntervalTrigger`, and added a required `UserItemData` argument to `BaseItem.IsPlayed`.
- Impact: The plugin will not load on a server outside its declared ABI, and will not compile against the next one without changes.
- Migration plan: On each Jellyfin minor release, bump both package versions and `targetAbi` together and rebuild. The analyzer stack plus `TreatWarningsAsErrors` surfaces most breaks at compile time.

**Aging test dependencies:**
- Risk: `xunit` 2.4.1, `Microsoft.NET.Test.Sdk` 17.2.0, and `coverlet.collector` 3.1.2 are several major versions behind.
- Impact: Low today; the suite runs clean. Renovate is configured and should propose upgrades.
- Migration plan: Take the Renovate PRs; xunit v3 is a larger migration worth doing deliberately.

**Local toolchain workaround:**
- Risk: `mise.toml` pins .NET 10 while the projects target `net9.0`, requiring `DOTNET_ROLL_FORWARD=LatestMajor` for `dotnet test` to run at all.
- Impact: Anyone running `dotnet test` without the mise environment hits a confusing missing-framework error.
- Migration plan: Either install the .NET 9 runtime alongside, or move the test project to `net10.0` once the plugin's target framework is decoupled from the test host.

## Missing Critical Features

**No way to manage playlists from the server UI:**
- Problem: Definitions are hand-written JSON files placed in a data directory. There is no configuration page, no API, and no validation feedback.
- Blocks: Non-technical use entirely. Also means malformed JSON surfaces only as a task failure in the server log.

**No validation of definitions:**
- Problem: `SmarterPlaylist` has no validation step; a bad member or operator name throws only when the rule is compiled, mid-run.
- Blocks: Useful error reporting. A single malformed definition throws out of `RefreshPlaylistAsync` and aborts the entire task run, so one bad file stops every other playlist from refreshing.

**Limited filterable properties and sort orders:**
- Problem: `Operand` exposes ~20 properties; `BaseItem` offers many more that users would reasonably want (`ProductionYear`, `OfficialRating`, `Tags`, `RunTimeTicks`, `SeriesName`). Only three sort orders exist.
- Blocks: Both are listed in the README's future work and are cheap to extend — a new `Operand` property is picked up by the engine automatically, and a new order is one file plus one switch arm.

## Test Coverage Gaps

**The entire refresh pipeline:**
- What's not tested: Playlist creation, id matching, item removal, and repopulation. This is the code that actually mutates user data.
- Files: `Jellyfin.Plugin.SmarterPlaylist/ScheduleTasks/RefreshAllPlaylists.cs`
- Risk: High. The dash-stripping id comparison and the direct `LinkedChildren` rewrite are both fragile and both silent on failure.
- Priority: High. Blocked on making the store injectable (see Tech Debt) and on mocking the Jellyfin manager interfaces.

**Store and filesystem layers:**
- What's not tested: JSON load/save round-trip against a real temp directory, and directory creation on first use.
- Files: `Jellyfin.Plugin.SmarterPlaylist/SmarterPlaylistStore.cs`, `Jellyfin.Plugin.SmarterPlaylist/SmarterPlaylistFileSystem.cs`
- Risk: Medium. DTO-level serialization is covered by `SmarterPlaylistDtoTest.cs`, but nothing exercises the file I/O path.
- Priority: Medium. Straightforward to add — no Jellyfin services are involved.

**`OperandFactory` projection:**
- What's not tested: The mapping from `BaseItem` to `Operand`, including the `PersonKind` filtering that was silently broken before the 10.11 migration (it compared the enum against strings, so every person collection came back empty).
- Files: `Jellyfin.Plugin.SmarterPlaylist/QueryEngine/OperandFactory.cs`
- Risk: Medium-high. This exact class already shipped one silent, total-failure bug of this kind.
- Priority: Medium. Requires mocking `ILibraryManager` and `IUserDataManager`.

**Regex-on-collection behavior:**
- What's not tested: There is deliberately no test asserting the current broken behavior. `EngineTest.cs` covers regex only against string properties.
- Files: `Jellyfin.Plugin.SmarterPlaylist/QueryEngine/Engine.cs`
- Risk: The gap is intentional — add the test alongside the fix, asserting correct behavior.
- Priority: High, paired with the bug fix.

---

*Concerns audit: 2026-07-25*
