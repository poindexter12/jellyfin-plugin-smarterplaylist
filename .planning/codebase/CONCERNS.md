# Codebase Concerns

**Analysis Date:** 2026-07-25
**Last revised:** 2026-07-29 — the configuration page, HTTP API, validation layer and operator registry all landed after the original audit, which invalidated several entries below. Each was re-checked against the code rather than assumed still true.

Every item below was verified against the code in this repository. Items marked **CONFIRMED BY TEST** were reproduced with an executable check rather than inferred by reading.

## Tech Debt

**Store API is partly dead code (revised 2026-07-29):**
- Issue: two of the five `ISmarterPlaylistStore` members still have no callers anywhere in the repo — `GetSmarterPlaylistAsync(Guid)` and `LoadPlaylistsAsync(Guid userId)`. Both are keyed by `Guid`, which is the API shape the plugin turned out not to want.
- Files: `Jellyfin.Plugin.SmarterPlaylist/ISmarterPlaylistStore.cs`, `Jellyfin.Plugin.SmarterPlaylist/SmarterPlaylistStore.cs`
- Resolved since the audit: `Delete` is now called by `SmarterPlaylistController.cs:551`, so definitions are cleaned up through the config page. `GetAllSmarterPlaylistsAsync` and `SaveAsync` are used by the scheduled task and `PlaylistSynchronizer`.
- Impact: unused surface implies capability that does not exist. `LoadPlaylistsAsync(userId)` is the more misleading of the two, since it also ignores its argument — see the partitioning entry below.
- Fix approach: delete both. The management surface that arrived addresses definitions by file name, not `Guid`, so nothing is waiting on them.

**Per-user partitioning is implied but not implemented:**
- Issue: `GetSmarterPlaylistFilePaths(string userId)` and `GetSmarterPlaylistPath(string userId, string playlistId)` accept a `userId` and ignore it entirely. All definitions live flat in one directory keyed only by file name.
- Files: `Jellyfin.Plugin.SmarterPlaylist/SmarterPlaylistFileSystem.cs`
- Impact: Two users cannot own same-named definitions, and `LoadPlaylistsAsync(userId)` would return every user's playlists rather than that user's. Ownership lives only inside each file's `User` field.
- Fix approach: Either partition into `<BasePath>/<userId>/` subdirectories, or drop the parameters so the API stops lying about its behavior.

**Store is constructed rather than injected — FIXED:**
- Issue: `RefreshAllPlaylists` newed up `SmarterPlaylistStore` and `SmarterPlaylistFileSystem` in its own constructor instead of receiving `ISmarterPlaylistStore` from DI, so the task could not be tested against a fake store.
- Files: `Jellyfin.Plugin.SmarterPlaylist/PluginServiceRegistrator.cs`, `Jellyfin.Plugin.SmarterPlaylist/ScheduleTasks/RefreshAllPlaylists.cs`
- Resolution: `PluginServiceRegistrator` registers `ISmarterPlaylistStore`, `ISmarterPlaylistFileSystem`, `IRefreshStatusStore`, `IPlaylistCoverStore`, `IPlaylistCoverService` and `IPlaylistSynchronizer`; the task takes all six of its collaborators as constructor parameters.
- Residual: the refresh flow is still not covered end to end, but it is no longer *blocked* on this — see Test Coverage Gaps.

**Template leftovers in repo configuration — FIXED 2026-07-25:**
- Issue: `scan-codeql.yaml` **and** `changelog.yaml` both passed `repository-name: jellyfin/jellyfin-plugin-template` instead of this repository, so CodeQL results and generated changelogs were attributed to the wrong name. `README.md` linked to `blob/master` while the default branch is `main`, so the `Operand` link 404'd.
- Files: `.github/workflows/scan-codeql.yaml`, `.github/workflows/changelog.yaml`, `README.md`
- Resolution: Both corrected. The sibling leftovers in `build.yaml` — placeholder overview, description, and changelog — were corrected during the 10.11 migration.
- Watch for: Other `@master` references in `.github/workflows/` are **correct** — they pin the upstream `jellyfin/jellyfin-meta-plugins` reusable workflows, whose default branch really is `master`. Do not "fix" those.

**No plugin configuration page — FIXED:**
- Issue: `Plugin` previously implemented `IHasWebPages` but returned a single `PluginPageInfo` with its `EmbeddedResourcePath` commented out, registering an empty page. The interface was removed rather than left as a broken stub.
- Files: `Jellyfin.Plugin.SmarterPlaylist/Plugin.cs`, `Jellyfin.Plugin.SmarterPlaylist/Configuration/configPage.html`
- Resolution: `Plugin` implements `IHasWebPages` again (`Plugin.cs:14`) and `GetPages` returns a real embedded resource. The page is backed by `SmarterPlaylistController`, which serves the schema and offers list, read, create, update, delete, validate, preview and field-value endpoints.

## Known Bugs

**`MaxItems` was a silent no-op — FIXED 2026-07-25:**
- Symptoms: A definition setting `MaxItems` had no effect; playlists were never truncated.
- Files: `Jellyfin.Plugin.SmarterPlaylist/SmarterPlaylist.cs`, `Jellyfin.Plugin.SmarterPlaylist/SmarterPlaylistDto.cs`
- Cause: The value was parsed, defaulted to `DefaultMaxItems` (1000), and exposed as a property, but `FilterPlaylistItems` never applied a `Take`. The field was also absent from the README, so it was both non-functional and undocumented.
- Resolution: `FilterPlaylistItems` now applies `.Take(MaxItems)` after ordering, so the cap keeps the first N in the chosen sort order. `MaxItems` is now documented in `README.md`.
- Residual risk: none remaining. The cap was untested at the time of the audit because `FilterPlaylistItems` required `ILibraryManager`/`IUserDataManager` doubles; it now takes flattened `PlaylistCandidate` records and is covered by `FilterAndOrderTest`. See *`MaxItems` enforcement — CLOSED 2026-07-26* under Test Coverage Gaps.

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
- Resolution: wrapped in an `InvalidOperationException` naming the file path. This only improved diagnosis; the blast-radius problem was fixed separately — see *One bad definition aborted every other playlist's refresh* below.

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
- Resolution: empty names and anything containing a path separator are now rejected (`SmarterPlaylistFileSystem.cs:70`, `Path.GetFileName(playlistId) != playlistId`). Covered by `SmarterPlaylistFileSystemTest`.
- **The escalation path this anticipated is now live.** `SmarterPlaylistController` takes `fileName` from the route on `GET`/`PUT`/`DELETE Definitions/{fileName}`, so the guard is load-bearing rather than defence in depth. Do not weaken it, and do not add a second path-building route that bypasses it.

**A list operator could mutate the item it was testing — FIXED 2026-07-29:**
- Symptoms: A rule such as `{"MemberName": "Genres", "Operator": "Remove", "TargetValue": "Comedy"}` compiled successfully and removed the value from the operand while evaluating it.
- Files: `Jellyfin.Plugin.SmarterPlaylist/QueryEngine/Engine.cs` (`BuildExpr`)
- Cause: an operator was resolved with `tProp.GetMethod(r.Operator)`, so the accepted vocabulary was whatever the member's CLR type exposed. `Collection<string>.Remove(string)` returns `bool`, which type-checks as a predicate.
- **CONFIRMED BY TEST** against the pre-fix code: `Remove compiled. result=True genresAfter=[Drama]` from a starting `[Comedy, Drama]`. `Clear` threw `IndexOutOfRangeException` from `GetParameters()[0]`; `CompareTo` threw `ArgumentException` on the `int` return.
- Severity: bounded. `Operand` is a per-refresh projection, so the mutation could not reach the library or the definition file — a robustness bug, not a security one. Nothing advertised these operators, but a hand-written definition reached them directly and the config page's validation could not flag what its schema never listed.
- Resolution: operators are now declared in `OperatorRegistry` rather than reflected. Covered by `EngineTest.MutatingMethodsAreNotOperators` for `Remove`, `Add`, `Clear` and `Insert`.

**Validation rejected the relative dates the config page itself produced — FIXED 2026-07-29:**
- Symptoms: `now-30d` — exactly what the page's own relative date mode writes — was reported as `E09: 'now-30d' is neither a date nor a Unix timestamp`.
- Files: `Jellyfin.Plugin.SmarterPlaylist/Api/DefinitionValidator.cs`, `Jellyfin.Plugin.SmarterPlaylist/QueryEngine/Engine.cs`
- Cause: the validator judged date values with `DateTime.TryParse` and a numeric fallback, neither of which recognises an offset. The engine had `_relativeDate` for exactly this but kept it private, so the two components disagreed about what a date is.
- **CONFIRMED BY TEST** against the pre-fix code in a throwaway worktree: `BASELINE E09: 'now-30d' is neither a date nor a Unix timestamp.`
- Resolution: `Engine.IsRelativeDate` is public and both callers use it. Covered by `DefinitionValidatorTest.RelativeDatesAreAccepted`.
- Note: the bug existed from the moment relative dates shipped (#31) and survived a release, because no validator test covered the syntax the feature added.

**One bad definition aborted every other playlist's refresh — FIXED:**
- Symptoms: A single unknown member, bad operator or unparseable date froze every other playlist, since the exception propagated out of the per-definition loop and ended the task run.
- Files: `Jellyfin.Plugin.SmarterPlaylist/ScheduleTasks/RefreshAllPlaylists.cs`
- Resolution: each definition is now synchronized inside a `try`/`catch` that excludes only `OperationCanceledException`, logs the failure against the definition's name, and records a `RefreshOutcome.Failed` status. The breadth of the catch is deliberate and documented at the call site: the failure modes are open-ended, and narrowing it would let an unanticipated exception type reintroduce the bug.

## Security Considerations

**User-supplied regular expressions:**
- Risk: Playlist definitions are user-authored and their `TargetValue` is compiled into a `Regex`. A catastrophically backtracking pattern could stall the scheduled task.
- Files: `Jellyfin.Plugin.SmarterPlaylist/QueryEngine/Engine.cs`
- Current mitigation: A 5-second `Regex` match timeout is applied at construction.
- Recommendations: Consider surfacing `RegexMatchTimeoutException` as a per-playlist error rather than letting it fail the whole task run.

**Runtime expression compilation:**
- Risk: `Engine.CompileRule` calls `Compile(true)`, emitting IL from user-controlled member names and operator names.
- Files: `Jellyfin.Plugin.SmarterPlaylist/QueryEngine/Engine.cs`, `Jellyfin.Plugin.SmarterPlaylist/QueryEngine/Operators/OperatorRegistry.cs`
- Current mitigation (revised 2026-07-29): member names are still resolved by reflection against `Operand`, so the member surface is bounded by its properties. **Operator names no longer are.** They are looked up in `OperatorRegistry` keyed by name *and* member kind, and an unmatched name throws `ArgumentException` naming the valid alternatives. Previously the operator surface was every method on the member's CLR type — see the mutating-method bug above.
- Recommendations: Acceptable as-is, and materially narrower than before. Note that `Compile(true)` forces a dynamic assembly, which is unavailable under full AOT should Jellyfin ever move that way.

**Definitions are read from a server-writable directory:**
- Risk: Anyone who can write to `<DataPath>/SmarterPlaylists/` controls which user each playlist targets, since ownership is just the `User` string in the file.
- Files: `Jellyfin.Plugin.SmarterPlaylist/SmarterPlaylistFileSystem.cs`
- Current mitigation: Filesystem permissions on the Jellyfin data directory.
- Recommendations: Same trust boundary as the rest of the server's data directory; no additional control needed unless a web-based editor is added, at which point authorization becomes essential.

## Performance Bottlenecks

**Full library scan per playlist, per run — FIXED:**
- Problem: every refresh enumerated the entire user-visible library once per playlist definition rather than once per user.
- Files: `Jellyfin.Plugin.SmarterPlaylist/ScheduleTasks/RefreshAllPlaylists.cs` (`CandidatesFor`)
- Resolution: candidates are memoized in a `candidatesByUser` dictionary keyed by user id and built once per user per run. `NeededMembersByUser` first computes the union of members every one of that user's definitions reads, so the single fetch is projected once with everything any of them will need — rather than per definition with only its own.

**Operand construction hits the library per item — FIXED:**
- Problem: `OperandFactory` called `ILibraryManager.GetPeople(baseItem)` for every candidate item, for every playlist, even when no rule referenced a person.
- Files: `Jellyfin.Plugin.SmarterPlaylist/QueryEngine/OperandFactory.cs`
- Resolution: projection takes a `needed` set of member names. `GetPeople` runs only when `needed` intersects `_peopleMembers`, and the user-data lookup only when it intersects `_userDataMembers`. Passing `null` still fills everything, which is what keeps the factory usable outside the refresh path.
- Watch for: a new `Operand` member sourced from people or user data must be added to `_peopleMembers` / `_userDataMembers`, or it silently projects as empty whenever a rule reads it alone. The member lists are the coupling that makes this optimisation correct, and nothing enforces them.

**Rules recompiled every run:**
- Problem: Expression trees are rebuilt and JIT-compiled on each execution.
- Files: `Jellyfin.Plugin.SmarterPlaylist/SmarterPlaylist.cs` (`CompileRuleSets`)
- Cause: A fresh `SmarterPlaylist` is constructed per definition per run.
- Improvement path: Cache compiled delegates keyed by definition content hash. Low priority — compilation cost is small next to the library scan.

## Fragile Areas

**`Operand` is a string-bound public contract (revised 2026-07-29):**
- Files: `Jellyfin.Plugin.SmarterPlaylist/QueryEngine/Operand.cs`, `Jellyfin.Plugin.SmarterPlaylist/QueryEngine/MemberClassifier.cs`
- Why fragile: `Operand`'s property names and CLR types are the plugin's public contract, bound by string from user JSON with no compile-time link. Renaming a property, or changing its type, silently breaks every playlist file using it — and retyping one now also silently changes which operators the member offers, since `MemberClassifier` derives the kind from the CLR type.
- **No longer fragile:** which operators are legal used to be an emergent consequence of the property's type. It is now declared in `OperatorRegistry`, and adding a member to `Engine.DateMembers` or changing a type moves a member between declared vocabularies rather than into an undeclared one.
- Safe modification: Add properties freely; never rename or retype an existing one without a migration. `SchemaBuilderTest.EveryAdvertisedOperatorCompiles` fails if a type change leaves the schema advertising something the engine rejects.
- Test coverage: Good (operator/kind matrix, unknown member, unknown operator, operator valid for another kind, date rewriting, mutating methods rejected).

**Operator arity is encoded in a single string field:**
- Files: `Jellyfin.Plugin.SmarterPlaylist/QueryEngine/Operators/RuleValueList.cs`, `Jellyfin.Plugin.SmarterPlaylist/QueryEngine/Operators/ValueArity.cs`
- Why fragile: `Between`, `AnyOf` and `NoneOf` pack several values into the one `TargetValue` string, comma-separated with `\,` as the escape. This was chosen over adding a `TargetValues` array so that no existing definition file or DTO changed shape, but it means a value containing a real comma depends on the author knowing to escape it. `AnyOfOperator.ValidateValue` catches the empty-part case (`E17`); it cannot catch a genre legitimately named with a comma that the user did not escape — that silently becomes two candidates matching nothing.
- Safe modification: Any new multi-value operator must declare its `Arity` and go through `RuleValueList`, never split the string itself. The config page reads arity from `SchemaResponse.Operators` to pick its control, so an operator that lies about its arity renders the wrong input.
- Test coverage: Good for splitting, joining, round-tripping and escaping (`OperatorRegistryTest`). No test asserts what happens to an *unescaped* comma, because the behaviour is ambiguous by construction rather than wrong.

**DTO/JSON binding contract:**
- Files: `Jellyfin.Plugin.SmarterPlaylist/SmarterPlaylistDto.cs`, `Jellyfin.Plugin.SmarterPlaylist/ExpressionSet.cs`
- Why fragile: Collection properties are get-only to satisfy CA2227, which means `System.Text.Json` will silently skip them unless `[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]` is present on the type. Removing that attribute produces empty playlists with no error at any layer.
- Safe modification: Keep the attribute whenever a collection property is get-only. `SmarterPlaylistDtoTest.cs` loads the exact JSON documented in the README and will fail if this regresses.
- Test coverage: Good — deserialization, round-trip, and missing-collection defaults are all covered.

**Playlist entry removal reimplemented a working platform API — FIXED 2026-07-25:**
- Files: `Jellyfin.Plugin.SmarterPlaylist/PlaylistSynchronizer.cs` (moved out of `ScheduleTasks/RefreshAllPlaylists.cs` since the audit)
- Why it was fragile: A private `RemoveFromPlaylist` reached past the public API, rewriting `playlist.LinkedChildren` directly and calling `UpdateToRepositoryAsync` itself. A source comment justified this: *"Real PlaylistManagers RemoveFromPlaylist needs an entry ID which seems to not work."*
- Root cause (verified against Jellyfin v10.11.11 `Emby.Server.Implementations/Playlists/PlaylistManager.cs`): that diagnosis was wrong. `IPlaylistManager.RemoveItemFromPlaylistAsync(string playlistId, IEnumerable<string> entryIds)` matches on `i.Item1.ItemId?.ToString("N", CultureInfo.InvariantCulture)` — the **undashed** id format. The plugin was passing dashed (`"D"`) ids, so nothing ever matched, and the author concluded the API was broken rather than that the format was wrong. Jellyfin's implementation was otherwise line-for-line what the plugin hand-rolled.
- Resolution: `RemoveFromPlaylist` deleted; the refresh now calls `_playlistManager.RemoveItemFromPlaylistAsync` with ids formatted via `ToString("N", CultureInfo.InvariantCulture)`. This also removed the `IFileSystem` and `IProviderManager` constructor dependencies, since Jellyfin queues the metadata refresh internally — the task now takes six injected services instead of eight.
- Residual risk: Still untested end to end. The `"N"` format requirement is load-bearing and invisible — passing the dashed form silently removes nothing, leaving stale items in the playlist. A code comment records this at the call site.

**Playlist id format is load-bearing and undocumented:**
- Files: `Jellyfin.Plugin.SmarterPlaylist/PlaylistSynchronizer.cs` (moved out of `ScheduleTasks/RefreshAllPlaylists.cs` since the audit; the `"N"` requirement is recorded in a comment at `PlaylistSynchronizer.cs:116`)
- Why fragile: `FindPlaylists` compares `playlist.Id.ToString()` with dashes stripped against the definition's stored `Id`. This is **correct** — Jellyfin's `CreatePlaylist` returns `playlist.Id.ToString("N")`, so stored ids are undashed — but nothing in the code states that invariant. A future edit that stores a dashed id, or drops the `Replace`, would silently stop matching and make the plugin create a duplicate playlist on every run.
- Safe modification: Parse both sides as `Guid` and compare, which is format-agnostic and self-documenting.
- Test coverage: None.

## Scaling Limits

**Library size (revised 2026-07-29):**
- Current capacity: Fine for typical personal libraries.
- Limit: cost is now O(users x library items) for the fetch plus O(definitions x candidates) for evaluation, against a default 30-minute trigger. The fetch — the expensive half — was O(definitions x library items) at the time of the audit.
- Scaling path: the per-user batching this entry called for has landed (see Performance). What remains is incremental refresh driven by library-change events rather than a fixed interval, which is the only thing that removes the repeated full scan entirely.

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

**No way to manage playlists from the server UI — DELIVERED:**
- Problem was: definitions were hand-written JSON files in a data directory, with no configuration page, no API and no validation feedback.
- Now: `configPage.html` is a visual rule builder — property, operator and value as dropdowns with AND/OR groups — over `SmarterPlaylistController` (schema, list, read, create, update, delete, validate, preview, field values). Value pickers are drawn from the target user's real library.
- Residual: the page is reachable only from the dashboard, so definitions are still administered by someone naming a target user. Per-user self-service remains open and is the README's last future-work item.

**No validation of definitions — DELIVERED:**
- Problem was: `SmarterPlaylist` had no validation step, so a bad member or operator name threw only when the rule was compiled, mid-run.
- Now: `DefinitionValidator` produces coded diagnostics (`E01`–`E17`, `W01`) on demand via `POST Validate`, ahead of any refresh. Operator-specific value checks are delegated to the operator itself, so the validator no longer holds a second, always-incomplete copy of what each one accepts.
- Residual: validation is advisory at the HTTP layer. A definition written straight to disk is still only checked when it compiles — which is now survivable, since one failure no longer aborts the run.

**Limited filterable properties and sort orders (revised 2026-07-29):**
- Properties: largely addressed. Every field the original audit named has landed — `ProductionYear`, `OfficialRating`, `Tags`, `RunTimeMinutes` and `SeriesName` are all on `Operand`, alongside `SeasonName`, `SeasonNumber`, `EpisodeNumber`, `LastPlayedDate` and the four `Date*` members. What remains is whatever Jellyfin exposes that nobody has asked for.
- Operators: addressed. Seven landed with the registry (`Between`/`NotBetween`, `AnyOf`/`NoneOf`, `ContainsIgnoreCase`, `NotContains`, `IsEmpty`/`IsNotEmpty`), and adding another is now one class implementing `IRuleOperator` — picked up by the engine, the schema and validation together.
- **Sort orders are the part still genuinely limited:** four exist (`NoOrder`, `Release Date Ascending`, `Release Date Descending`, `Series, Season, Episode`). Sort by name, date added and rating are all still missing.
- The *drift* around orders is fixed, though the shortage is not. `OrderRegistry` is now the single source: `SmarterPlaylist` resolves through it and `SchemaBuilder` reads its `Names`, so adding an order is a class plus one registry entry rather than three edits across three files. The two silent failures it used to allow — a class with no switch arm being offered but falling back to library order, and a working order missing from the schema array — are both covered by `OrderRegistryTest`, whose guards were confirmed to fail against a deliberately mis-wired entry rather than assumed to work.
- `OrderDto`'s doc comment used to name three of the four orders, and had done since the fourth was added. It now points at the registry instead of carrying a fourth copy of the list.

## Test Coverage Gaps

**The refresh pipeline (revised 2026-07-29):**
- What's not tested: playlist creation, id matching, item removal and repopulation — the code that actually mutates user data. `PlaylistSynchronizer` now holds most of it, extracted from the scheduled task since the audit, and has no tests of its own.
- Files: `Jellyfin.Plugin.SmarterPlaylist/PlaylistSynchronizer.cs`, `Jellyfin.Plugin.SmarterPlaylist/ScheduleTasks/RefreshAllPlaylists.cs`
- Risk: High, and the highest remaining gap in the repo. The dash-stripping id comparison is silent on failure, and the `"N"`-format requirement for `RemoveItemFromPlaylistAsync` is load-bearing and invisible.
- Priority: High. **No longer blocked** — the DI work landed and `IPlaylistSynchronizer` is an interface the task consumes, so the task is testable against a fake, and the synchronizer is testable against mocked Jellyfin managers. What is left is the work of writing them.
- Partial coverage exists either side of it: `RefreshStatusStoreTest` covers status recording, `PlaylistCoverTest` the cover pipeline, and `FilterAndOrderTest` the selection and capping the synchronizer calls into.

**Store layer — CLOSED:**
- What's now tested: `SmarterPlaylistStoreTest` exercises the file I/O path against a real temp directory, including that saved definitions stay readable rather than minified and that `FileName` is taken from disk so a divergent field cannot fork a definition.
- Files: `Jellyfin.Plugin.SmarterPlaylist/SmarterPlaylistStore.cs`, `Jellyfin.Plugin.SmarterPlaylist.Tests/SmarterPlaylistStoreTest.cs`
- **Unblocked 2026-07-25:** the test project references `Jellyfin.Controller`/`Jellyfin.Model` *without* `ExcludeAssets=runtime`. The plugin excludes them because the server supplies them at runtime, but that also made every Jellyfin type fail to load under the test host — which was the real reason the Jellyfin-facing code had no coverage, rather than a deliberate choice.

**`OperandFactory` projection:**
- What's not tested: The mapping from `BaseItem` to `Operand`, including the `PersonKind` filtering that was silently broken before the 10.11 migration (it compared the enum against strings, so every person collection came back empty).
- Files: `Jellyfin.Plugin.SmarterPlaylist/QueryEngine/OperandFactory.cs`
- Risk: Medium-high. This exact class already shipped one silent, total-failure bug of this kind.
- Priority: Medium. Requires mocking `ILibraryManager` and `IUserDataManager`.

**`MaxItems` enforcement — CLOSED 2026-07-26:**
- What's now tested: `FilterAndOrderTest` asserts the cap applies *after* sorting (so the first N in the chosen order survive), that `MaxItems: 0` falls back to the 1000 default, and that `MatchedCount`/`Truncated` still report the pre-cap total.
- Files: `Jellyfin.Plugin.SmarterPlaylist/SmarterPlaylist.cs`, `Jellyfin.Plugin.SmarterPlaylist.Tests/FilterAndOrderTest.cs`
- Resolution: this was never really blocked on mocking. `FilterPlaylistItems` took Jellyfin entities plus the two managers needed to project them, which is what made it unreachable from a test. It now takes flattened `PlaylistCandidate` records, so selection, ordering and capping are all exercised directly with no doubles at all. Ordering was covered by the same change.

**Documented operator matrix (revised 2026-07-29):**
- What's now tested: `SchemaBuilderTest` pins each kind's operator list exactly, and `EveryAdvertisedOperatorCompiles` proves the schema cannot advertise anything the engine would reject. That test now samples a value shaped to each operator's arity, so it still covers the multi-value operators rather than passing them a single value they would legitimately refuse. `EveryAdvertisedOperatorIsDescribed` catches an operator offered on a member but missing from `SchemaResponse.Operators`, which would leave the page with no way to choose an input for it.
- Files: `Jellyfin.Plugin.SmarterPlaylist/QueryEngine/Operators/OperatorRegistry.cs`, `Jellyfin.Plugin.SmarterPlaylist.Tests/SchemaBuilderTest.cs`, `README.md`
- Risk: Low. The README table and the schema are both derived from one registry, so they can only disagree if the README is edited by hand and not re-checked.
- Priority: Maintain. The README table is *not* generated, so adding an operator means updating it as well as the registry. Nothing enforces that.

**Config page JavaScript:**
- What's not tested: `configPage.html` carries roughly 500 lines of logic — control selection by member kind and operator arity, date composition and decomposition, value-picker loading and hinting, DTO read/write — none of which is exercised by anything.
- Files: `Jellyfin.Plugin.SmarterPlaylist/Configuration/configPage.html`
- Risk: Medium. The arity branch added on 2026-07-29 decides whether a value box is numeric, free text or disabled, and clears the value when arity changes. A mistake there silently saves a value in the wrong shape, which validation would then reject at the server — recoverable, but confusing.
- Priority: Low-medium. There is no JS test harness in the repo and adding one for a single embedded page is a disproportionate amount of new infrastructure. The server-side guarantees are the real safety net: anything the page can produce is validated before it is saved.

---

*Concerns audit: 2026-07-25. Revised 2026-07-29 for the configuration page, HTTP API, validation layer and operator registry.*
