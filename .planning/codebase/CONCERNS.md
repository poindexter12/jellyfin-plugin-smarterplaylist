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

**Template leftovers in repo configuration — FIXED 2026-07-25:**
- Issue: `scan-codeql.yaml` **and** `changelog.yaml` both passed `repository-name: jellyfin/jellyfin-plugin-template` instead of this repository, so CodeQL results and generated changelogs were attributed to the wrong name. `README.md` linked to `blob/master` while the default branch is `main`, so the `Operand` link 404'd.
- Files: `.github/workflows/scan-codeql.yaml`, `.github/workflows/changelog.yaml`, `README.md`
- Resolution: Both corrected. The sibling leftovers in `build.yaml` — placeholder overview, description, and changelog — were corrected during the 10.11 migration.
- Watch for: Other `@master` references in `.github/workflows/` are **correct** — they pin the upstream `jellyfin/jellyfin-meta-plugins` reusable workflows, whose default branch really is `master`. Do not "fix" those.

**No plugin configuration page:**
- Issue: `Plugin` previously implemented `IHasWebPages` but returned a single `PluginPageInfo` with its `EmbeddedResourcePath` commented out, registering an empty page. The interface has been removed rather than left as a broken stub.
- Files: `Jellyfin.Plugin.SmarterPlaylist/Plugin.cs`
- Impact: All configuration is hand-edited JSON; there is no in-server UI.
- Fix approach: Re-add `IHasWebPages` together with a real embedded HTML resource when a config UI is actually built.

## Known Bugs

**`MaxItems` was a silent no-op — FIXED 2026-07-25:**
- Symptoms: A definition setting `MaxItems` had no effect; playlists were never truncated.
- Files: `Jellyfin.Plugin.SmarterPlaylist/SmarterPlaylist.cs`, `Jellyfin.Plugin.SmarterPlaylist/SmarterPlaylistDto.cs`
- Cause: The value was parsed, defaulted to `DefaultMaxItems` (1000), and exposed as a property, but `FilterPlaylistItems` never applied a `Take`. The field was also absent from the README, so it was both non-functional and undocumented.
- Resolution: `FilterPlaylistItems` now applies `.Take(MaxItems)` after ordering, so the cap keeps the first N in the chosen sort order. `MaxItems` is now documented in `README.md`.
- Residual risk: The cap itself is **not** covered by a test, because `FilterPlaylistItems` requires `ILibraryManager`/`IUserDataManager` doubles. Only the DTO-to-model mapping is asserted. See Test Coverage Gaps.

**`MatchRegex`/`NotMatchRegex` were broken on collection properties — FIXED 2026-07-25:**
- Symptoms: `MatchRegex` against `Directors`, `Genres`, `Actors`, `Composers`, `GuestStars`, `Producers`, or `Studios` never matched. `NotMatchRegex` against those same properties matched every item.
- Files: `Jellyfin.Plugin.SmarterPlaylist/QueryEngine/Engine.cs` (`BuildRegexExpr`)
- Cause: `BuildRegexExpr` resolved `ToString()` on the property type. For a collection that binds to `Object.ToString()`, so the pattern was tested against the CLR type name rather than the collection's contents.
- Severity: `NotMatchRegex` was the dangerous half — it silently returned `true` for everything, so a rule intended to exclude items excluded nothing. Pre-dated the 10.11 migration; behaved identically when these were `List<string>`.
- Resolution: `BuildRegexExpr` now detects `IEnumerable<string>` properties and emits `Enumerable.Any(collection, element => regex.IsMatch(element))`, so a rule matches when any element matches. Non-collection members keep the previous `ToString()` behavior.
- Verification: Covered by `EngineTest.RegexOperatorsMatchCollectionMembersElementWise`, `RegexAgainstCollectionDoesNotMatchTheClrTypeName`, and `RegexAgainstEmptyCollectionDoesNotMatch`. These fail against the old implementation.

**Hand-authored JSON was reflowed to minified JSON — FIXED 2026-07-25:**
- Symptoms: The first refresh after creating a definition rewrote the user's formatted file as a single minified line.
- Files: `Jellyfin.Plugin.SmarterPlaylist/SmarterPlaylistStore.cs` (`SaveAsync`)
- Resolution: serialized with `WriteIndented = true`. Covered by `SmarterPlaylistStoreTest.SavedDefinitionIsReadableRatherThanMinified`. This was the README's "Pretty Print JSON files" future-work item.

**A divergent FileName forked one definition into two — FIXED 2026-07-25:**
- Symptoms: A file `foo.json` containing `"FileName": "bar"` caused `bar.json` to be written on first save while `foo.json` remained, so one authored playlist silently became two — both enumerated, both refreshing, both fighting over the same Jellyfin playlist.
- Files: `Jellyfin.Plugin.SmarterPlaylist/SmarterPlaylistStore.cs`
- Cause: enumeration returns real file names, but `SaveAsync` writes to the path named by the free-text `FileName` field, and nothing reconciled the two.
- Resolution: `LoadPlaylistAsync` now takes `FileName` from the file name on disk, making the on-disk name the definition's single identity. Covered by `SmarterPlaylistStoreTest.FileNameIsTakenFromDiskSoADivergentFieldCannotForkTheDefinition`.

**Malformed definitions failed without naming the file — FIXED 2026-07-25:**
- Symptoms: A JSON typo surfaced as a raw `JsonException` citing only a byte offset, with no indication of which file was at fault — while aborting the refresh of every other playlist.
- Files: `Jellyfin.Plugin.SmarterPlaylist/SmarterPlaylistStore.cs`
- Resolution: wrapped in an `InvalidOperationException` naming the file path. Note this only improves diagnosis; the blast-radius problem below is the real fix and is still open.

**Date rules corrupted their own definition file — FIXED 2026-07-25:**
- Symptoms: A `PremiereDate` rule written as a readable date worked on the first refresh, then permanently aborted **every** playlist's refresh on all subsequent runs.
- Files: `Jellyfin.Plugin.SmarterPlaylist/QueryEngine/Engine.cs`, `Jellyfin.Plugin.SmarterPlaylist/SmarterPlaylist.cs`, `Jellyfin.Plugin.SmarterPlaylist/ScheduleTasks/RefreshAllPlaylists.cs`
- Cause: three separate behaviors compounding. `Engine.FixRuleSets` normalized the rule set **in place**, mutating the deserialized DTO's own `Expression` objects. `RefreshPlaylistAsync` constructs the `SmarterPlaylist` (triggering that mutation) and then calls `SaveAsync(dto)` when first creating the playlist, persisting the mutated values — so the user's `"2020-07-01"` became `"1593561600"` in their own file. On the next run `DateTime.Parse("1593561600")` threw `FormatException`, and because an exception in `RefreshPlaylistAsync` propagates out of `ExecuteAsync`, that aborted the entire task.
- Resolution: `NormalizeRuleSets`/`NormalizeRules` replace `FixRuleSets`/`FixRules` and return copies, leaving the caller's rule set untouched. Numeric values also pass through, so any file already corrupted by the old behavior keeps working.
- Verification: `EngineTest.NormalizationDoesNotMutateTheCallersRuleSet` and `RepeatedNormalizationIsStableAcrossASaveReloadCycle`, the latter simulating the full normalize/persist/reload/normalize cycle.
- Note: found by designing the config page against the real data shape, not by reading the code — two prior review passes over this constructor missed it.

**A bare year in a date rule silently meant 1970 — FIXED 2026-07-25:**
- Symptoms: `{"MemberName": "PremiereDate", "Operator": "GreaterThan", "TargetValue": "2020"}` compiled without error and matched almost the entire library.
- Files: `Jellyfin.Plugin.SmarterPlaylist/QueryEngine/Engine.cs`
- Cause: introduced by the date fix itself. `DateTime.TryParse("2020", InvariantCulture)` returns **false** (measured, not assumed), so a bare year fell through the numeric passthrough and was taken as a raw timestamp — 1970-01-01T00:33:40Z.
- Resolution: numeric values in 1000–9999 are rejected with an error telling the user to write a full date. As genuine timestamps that range spans the first three hours of 1970, so nothing real is lost. Rejecting rather than silently reinterpreting was deliberate: a second implicit rule is what created this bug in the first place.
- Note: the config-page spec places this check in a UI validator, which is not sufficient — hand-edited JSON is a first-class path and bypasses any UI. The guard belongs in the engine.

**Operand took nulls from null-oblivious Jellyfin members — FIXED 2026-07-25:**
- Symptoms: Any rule on `Album` or `FolderPath` threw `NullReferenceException` per item for anything that is not audio.
- Files: `Jellyfin.Plugin.SmarterPlaylist/QueryEngine/OperandFactory.cs`
- Cause: `MediaBrowser.Controller` is compiled **without** `<Nullable>enable</Nullable>`, so `BaseItem.Album`, `ContainingFolderPath` and `Name` are null-oblivious. Assigning them to non-nullable `Operand` strings produced no compiler diagnostic, but `Album` is null for every Movie and Episode — both supported item kinds.
- Resolution: null-coalesced to `string.Empty` at the projection boundary.
- Watch for: the analyzer stack **cannot** catch this class of bug. Any value taken from a Jellyfin API must be treated as nullable regardless of what the compiler says.

**FileName allowed path traversal — FIXED 2026-07-25:**
- Risk: `SmarterPlaylistDto.FileName` is user-supplied and became the file name unchecked in `GetSmarterPlaylistPath`, so a definition could be written outside `BasePath`.
- Files: `Jellyfin.Plugin.SmarterPlaylist/SmarterPlaylistFileSystem.cs`
- Resolution: empty names and anything containing a path separator are now rejected. Covered by `SmarterPlaylistFileSystemTest`.
- Escalation risk was limited today (writing a definition already requires access to the data directory), but this becomes externally reachable the moment the planned config page exposes `FileName` over HTTP.

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

**Playlist entry removal reimplemented a working platform API — FIXED 2026-07-25:**
- Files: `Jellyfin.Plugin.SmarterPlaylist/ScheduleTasks/RefreshAllPlaylists.cs`
- Why it was fragile: A private `RemoveFromPlaylist` reached past the public API, rewriting `playlist.LinkedChildren` directly and calling `UpdateToRepositoryAsync` itself. A source comment justified this: *"Real PlaylistManagers RemoveFromPlaylist needs an entry ID which seems to not work."*
- Root cause (verified against Jellyfin v10.11.11 `Emby.Server.Implementations/Playlists/PlaylistManager.cs`): that diagnosis was wrong. `IPlaylistManager.RemoveItemFromPlaylistAsync(string playlistId, IEnumerable<string> entryIds)` matches on `i.Item1.ItemId?.ToString("N", CultureInfo.InvariantCulture)` — the **undashed** id format. The plugin was passing dashed (`"D"`) ids, so nothing ever matched, and the author concluded the API was broken rather than that the format was wrong. Jellyfin's implementation was otherwise line-for-line what the plugin hand-rolled.
- Resolution: `RemoveFromPlaylist` deleted; the refresh now calls `_playlistManager.RemoveItemFromPlaylistAsync` with ids formatted via `ToString("N", CultureInfo.InvariantCulture)`. This also removed the `IFileSystem` and `IProviderManager` constructor dependencies, since Jellyfin queues the metadata refresh internally — the task now takes six injected services instead of eight.
- Residual risk: Still untested end to end. The `"N"` format requirement is load-bearing and invisible — passing the dashed form silently removes nothing, leaving stale items in the playlist. A code comment records this at the call site.

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

**Store layer:**
- What's not tested: JSON load/save round-trip against a real temp directory. `SmarterPlaylistFileSystem` is now covered by `SmarterPlaylistFileSystemTest`, but `SmarterPlaylistStore` is not.
- Files: `Jellyfin.Plugin.SmarterPlaylist/SmarterPlaylistStore.cs`
- Risk: Medium. DTO-level serialization is covered by `SmarterPlaylistDtoTest.cs`, but nothing exercises the file I/O path.
- Priority: Medium. Straightforward to add — no Jellyfin services are involved.
- **Unblocked 2026-07-25:** the test project now references `Jellyfin.Controller`/`Jellyfin.Model` *without* `ExcludeAssets=runtime`. The plugin excludes them because the server supplies them at runtime, but that also made every Jellyfin type fail to load under the test host — which is the real reason the Jellyfin-facing code had no coverage, rather than a deliberate choice. Tests touching Jellyfin types are now possible.

**`OperandFactory` projection:**
- What's not tested: The mapping from `BaseItem` to `Operand`, including the `PersonKind` filtering that was silently broken before the 10.11 migration (it compared the enum against strings, so every person collection came back empty).
- Files: `Jellyfin.Plugin.SmarterPlaylist/QueryEngine/OperandFactory.cs`
- Risk: Medium-high. This exact class already shipped one silent, total-failure bug of this kind.
- Priority: Medium. Requires mocking `ILibraryManager` and `IUserDataManager`.

**`MaxItems` enforcement:**
- What's not tested: That `FilterPlaylistItems` actually caps the result. Only the DTO-to-model mapping of the value is asserted.
- Files: `Jellyfin.Plugin.SmarterPlaylist/SmarterPlaylist.cs`
- Risk: Medium. The fix is one `.Take(MaxItems)` call, but a regression would silently produce oversized playlists — the same failure mode as the original bug.
- Priority: Medium. Blocked on the same mocking work as the refresh pipeline. A tautological test that re-implements `Take` was deliberately **not** added, since it would assert the test's own logic rather than the code's.

**Documented operator matrix:**
- What's now tested: Every row of the README's operator table is pinned by `EngineTest.cs` — collection `Contains` requiring whole elements, element-wise regex, binary equality on text, and `NotEqual` on booleans.
- Files: `Jellyfin.Plugin.SmarterPlaylist/QueryEngine/Engine.cs`, `README.md`
- Risk: Low, and deliberately so. If someone changes an `Operand` property's type, the corresponding test fails and points at the README row that also needs updating.
- Priority: Maintain. Add a test row whenever a README row is added.

---

*Concerns audit: 2026-07-25*
