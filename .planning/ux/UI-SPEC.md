# UI-SPEC: SmarterPlaylist Configuration Page

**Status:** draft
**Date:** 2026-07-25
**Implements:** `.planning/ux/UX-RESEARCH.md` M1–M3 (V1, full detail) and M4–M7 (forward-compatibility detail only)
**Consumers:** planner, executor, ui-checker, ui-auditor
**Surface:** `Jellyfin.Plugin.SmarterPlaylist/Configuration/configPage.html`, embedded resource, registered via `IHasWebPages`

---

## 0. How to read this document

Every claim about the Jellyfin platform in this spec is one of three kinds, and is labelled:

- **VERIFIED** — read from a source file in `jellyfin/jellyfin-web` or a first-party `jellyfin/jellyfin-plugin-*` repository during authoring. The citation is given.
- **NEEDS VERIFICATION** — plausible, commonly used, but not confirmed by reading source during authoring. Implementers must confirm before relying on it, and a stated fallback is given.
- **DERIVED** — a consequence of this repository's own code, cited to file and symbol.

Anything not labelled is a design decision made by this spec, not a platform fact.

---

## 1. Scope

### In scope — V1, shippable on its own

| ID | Recommendation | What this spec covers |
|----|---------------|----------------------|
| M1 | Config page with a definitions list | Full screen spec, all states, all columns |
| M2 | Validate on save, not at refresh time | Full validation model, error taxonomy, firing rules, save-blocking contract |
| M3 | Fix the one-bad-file-aborts-everything blast radius | The **UI surfacing** of it: where per-definition failure is captured, stored, and rendered |

V1 also includes, as the minimum needed to make M2 have a save path at all:

- A per-definition detail panel with a **read-only human-readable rule view** (renders the OR/AND structure faithfully)
- An **Advanced (JSON) editor** as the V1 editing surface
- A **New definition** flow seeded from a template

### In scope — forward-compatibility only (M4–M7)

Specified to the depth needed to prove V1 does not paint them into a corner. Not specified to implementation depth.

| ID | Recommendation |
|----|---------------|
| M4 | Flat-list-first rule builder |
| M5 | Live-ish match preview |
| M6 | Real delete flow |
| M7 | Library-sourced value pickers |

### Explicitly out of scope

N1 (per-item match explanation), N2 (plain-language sentence rendering), N3 (deeper nesting), N4 (new `Operand` properties / sort orders), N5 (per-user self-service), N6 (pretty-printed JSON on disk).

Section 14 lists the specific things V1 must not preclude, per out-of-scope item.

---

## 2. Hard platform constraints

These are given, not chosen. Design lives inside them.

1. **The page is an embedded HTML resource.** `Plugin.cs` must gain `IHasWebPages` and return a `PluginPageInfo` whose `EmbeddedResourcePath` is `$"{GetType().Namespace}.Configuration.configPage.html"`. The `.csproj` must add `<None Remove="Configuration\configPage.html" />` + `<EmbeddedResource Include="Configuration\configPage.html" />`.
   *VERIFIED* — pattern read from `jellyfin/jellyfin-plugin-ldapauth` `LDAP-Auth/LdapPlugin.cs` (`GetPages()`) and `LDAP-Auth/LDAP-Auth.csproj`.
   *DERIVED* — `Plugin.cs` in this repo currently does **not** implement `IHasWebPages`; a stub that registered an empty page was removed (`.planning/codebase/CONCERNS.md`, "No plugin configuration page"). Re-adding it is part of this work.

2. **The page renders inside the Jellyfin admin dashboard and must look native.** Use Jellyfin's own classes and custom elements only. No CSS framework. No CDN. No web fonts. No bundler. One `<script>` block, plain ES5-compatible-or-modern-vanilla JS, no modules (plugin config pages are injected as raw HTML, not through the app's module graph).

3. **Theming follows the server theme.** Never hardcode a colour. Use the `--jf-palette-*` custom properties (Section 3.3) or inherit.

4. **V1 is necessarily admin-scoped.** Plugin configuration pages live in the admin dashboard behind `Policies.RequiresElevation`. True per-user self-service is not reachable through this mechanism at all, regardless of the per-user-partitioning gap in `SmarterPlaylistFileSystem`. This settles UX-RESEARCH §5 assumption 5 / N5 **on technical grounds**. The UI's model is therefore: *an administrator manages definitions, each of which names a target Jellyfin user.* Do not re-open this.

5. **There is no HTTP API today.** `.planning/codebase/ARCHITECTURE.md` — "There is no request/response path — the plugin has no HTTP surface." Every interactive behaviour in this spec requires a new `ApiController`. Section 4 lists exactly which endpoint each feature costs.

6. **Rule semantics the UI must express faithfully** (*DERIVED* from `SmarterPlaylist.FilterPlaylistItems`: `compiledRules.Any(set => set.All(rule => rule(operand)))`):
   - `ExpressionSets` are **OR**'d.
   - `Expressions` inside one set are **AND**'d.
   - Zero `ExpressionSets` → `Any` over empty → **matches nothing**.
   - An `ExpressionSet` with zero `Expressions` → `All` over empty → **matches everything**.
   - Operator legality is a consequence of the `Operand` property's CLR type (`Engine.BuildExpr`), not a declared contract.
   - On a list property, `Contains` is `Collection<string>.Contains` — a **whole exact element**, ordinal, case-sensitive.
   - On a list property, regex is element-wise: `MatchRegex` = any element matches; `NotMatchRegex` = no element matches (`Engine.BuildRegexExpr`).

---

## 3. Verified platform inventory

Only these primitives may be used. Anything not listed here is either forbidden or must be added to this list after verification.

### 3.1 Page shell and layout classes — VERIFIED

Read from `jellyfin/jellyfin-plugin-tvdb` `Jellyfin.Plugin.Tvdb/Configuration/config.html` and `jellyfin/jellyfin-plugin-ldapauth` `LDAP-Auth/Config/configPage.html`.

| Class / attribute | Purpose |
|---|---|
| `data-role="page" class="page type-interior pluginConfigurationPage"` | Root element of a plugin config page |
| `data-role="content"` | Content wrapper |
| `content-primary` | Primary column |
| `verticalSection`, `verticalSection-extrabottompadding` | Section container |
| `sectionTitleContainer flex align-items-center` + `h2.sectionTitle` | Section heading row |
| `inputContainer` | Wrapper for a labelled input |
| `fieldDescription` | Helper text under a control |
| `checkboxContainer`, `checkboxContainer-withDescription`, `checkboxFieldDescription` | Checkbox row |
| `selectContainer` | Wrapper for a select |
| `raised button-submit block` | Primary submit button styling |
| `raised button-alt` | Secondary button styling |
| `headerHelpButton` | Help link in a section header |
| `checkboxList paperList checkboxList-paperList` | Scrollable list-of-checkboxes container |

### 3.2 Custom elements — VERIFIED

Read from directory listing of `jellyfin/jellyfin-web` `src/elements/`.

| Element | Usage |
|---|---|
| `<input is="emby-input">` | Text / number / password inputs. Supports a `label` attribute. |
| `<select is="emby-select">` | Select. Supports a `label` attribute. |
| `<input type="checkbox" is="emby-checkbox">` | Checkbox, wrapped in a `<label>` |
| `<button is="emby-button">` / `<a is="emby-button">` | Buttons and button-styled links |
| `<button is="paper-icon-button-light">` | Icon-only button; the element adds class `paper-icon-button-light` (`src/elements/emby-button/paper-icon-button-light.js`) |
| `<div class="verticalSection" is="emby-collapse" title="…"><div class="collapseContent">…</div></div>` | Collapsible section |
| `<textarea is="emby-textarea">` | Multi-line text |

### 3.3 Theme tokens — VERIFIED

Read from `jellyfin/jellyfin-web` `src/themes/_base/_theme.scss` and `src/themes/_base/_palette.scss`. **This is the complete emitted set.** There is no success token and no warning token.

```
--jf-palette-background-default   --jf-palette-background-paper
--jf-palette-text-primary         --jf-palette-text-secondary
--jf-palette-divider              --jf-palette-action-hover
--jf-palette-action-focus         --jf-palette-action-selectedOpacity
--jf-palette-primary-main         --jf-palette-primary-dark
--jf-palette-primary-contrastText --jf-palette-primary-mainChannel
--jf-palette-error-main           --jf-palette-error-light
--jf-palette-error-contrastText   --jf-palette-common-white
--jf-palette-FilledInput-bg       --jf-palette-FilledInput-borderColor
--jf-palette-AppBar-defaultBg     --jf-card-borderRadius
```

Always write them with a fallback: `color: var(--jf-palette-text-secondary, #999);`

**Contrast finding — load-bearing.** `--jf-palette-error-main` is `#c62828` in both the light and dark themes (`_palette.scss` defines it; `src/themes/light/theme.scss` does **not** override it). Against the dark theme's paper background `#202020` (`src/themes/dark/theme.scss`, `--jf-palette-background-paperChannel, 32 32 32`) that measures **≈2.9:1** — below the 4.5:1 needed for text and below the 3:1 needed for a non-text UI indicator.

**Consequence, mandatory:** error colour may be used only as a decorative left border or background tint on a panel. It must never be the sole carrier of "this is an error", and error message text must be rendered in `--jf-palette-text-primary`. Every error state is conveyed by **icon + text label** as well.

### 3.4 Utility classes — VERIFIED

| Class | Source | Purpose |
|---|---|---|
| `.material-icons` | `src/styles/site.scss:68` | Material Icons ligature span |
| `.clipForScreenReader` | `src/styles/site.scss` | Visually hidden, screen-reader-available text |
| `.detailTable`, `.detailTableHeaderCell`, `.detailTableBodyCell` | `src/styles/detailtable.scss` | Dashboard table styling |

**NEEDS VERIFICATION:** that `detailtable.scss` is in scope on a plugin configuration page (it is a separate stylesheet from `site.scss`; whether it is bundled globally or route-scoped was not confirmed).
**Fallback, mandatory regardless:** the page ships a scoped `<style>` block (Section 3.6) that sets `border-collapse`, cell padding, and a `--jf-palette-divider` row rule for `#SmarterPlaylistPage .detailTable`, so the table is legible even if the class is not in scope. Use the class names anyway so the page inherits the dashboard look where it is available.

**NEEDS VERIFICATION:** the Material Icons ligature set bundled by `jellyfin-web` may be subsetted. Restrict icon usage to this list, all of which are core Material names: `check_circle`, `error`, `warning`, `schedule`, `edit`, `delete`, `add`, `refresh`, `expand_more`, `expand_less`, `visibility`, `open_in_new`. If a ligature renders as literal text at implementation time, fall back to a text-only status label — never to an image or an inline SVG icon set.

### 3.5 JavaScript globals — VERIFIED

Read from `jellyfin/jellyfin-web` `src/utils/dashboard.js` (which assigns `window.Dashboard`) and confirmed in use by both reference plugin pages.

| Global | Members this spec uses |
|---|---|
| `window.ApiClient` | `getPluginConfiguration(id)`, `updatePluginConfiguration(id, cfg)`, `getUsers()`, `getUrl(path, queryObj)`, `getJSON(url)`, `ajax({type, url, data, contentType})` |
| `window.Dashboard` | `showLoadingMsg()`, `hideLoadingMsg()`, `alert(stringOrOptions)`, `confirm(message, title, callback)`, `processPluginConfigurationUpdateResult()`, `processErrorResponse(response)`, `navigate(url)` |

Notes, all VERIFIED from `src/utils/dashboard.js`:
- `Dashboard.alert('text')` shows a **toast**; `Dashboard.alert({title, message, callback})` shows a **modal alert**. The two forms are not interchangeable — this spec states which is meant at every call site.
- `Dashboard.confirm(message, title, callback)` is callback-style, `callback(true|false)`. It is not a promise.
- `Dashboard.processPluginConfigurationUpdateResult()` hides loading and toasts the localised "Settings saved" string. It is only correct after a *plugin configuration* save. This plugin's definitions are **not** plugin configuration, so this spec does not use it; see Section 7.4.

Page lifecycle: bind on the `pageshow` event of the root page element, not `DOMContentLoaded`. *NEEDS VERIFICATION* of the exact event name on 10.11; both reference plugin pages bind on `pageshow`, so treat that as the primary and add a `DOMContentLoaded`-guarded one-shot init as a fallback if `pageshow` does not fire.

### 3.6 Scoped stylesheet budget

One `<style>` block inside the page root, every selector prefixed with `#SmarterPlaylistPage`. It may only:
- set layout (grid/flex, spacing, `overflow`, `min-height`),
- reference `--jf-palette-*` tokens with fallbacks,
- provide the `.detailTable` fallback described above,
- define `@media` breakpoints from Section 12.

It may **not** define a colour literal other than as a `var()` fallback, set a font family, or restyle any `emby-*` element.

---

## 4. Backend contract

This is real backend work. Each row is a commitment.

### 4.1 New endpoints

New file `Jellyfin.Plugin.SmarterPlaylist/Api/SmarterPlaylistController.cs`:

```csharp
[ApiController]
[Authorize(Policy = Policies.RequiresElevation)]   // MediaBrowser.Common.Api
[Route("SmarterPlaylist")]
[Produces(MediaTypeNames.Application.Json)]
public class SmarterPlaylistController : ControllerBase
```

*VERIFIED* — attribute set copied from `jellyfin-plugin-ldapauth` `LDAP-Auth/Api/LdapController.cs`.

| # | Method + route | Milestone | Consumed by | Response |
|---|---|---|---|---|
| E1 | `GET /SmarterPlaylist/Definitions` | M1 | S1 list | `{ BasePath, Definitions: DefinitionSummary[] }` |
| E2 | `GET /SmarterPlaylist/Definitions/{fileName}` | M1 | S2 detail | `DefinitionDetail` (parsed model + pretty-printed raw JSON + diagnostics + `SourceHash`) |
| E3 | `POST /SmarterPlaylist/Definitions` | M2 | S3 create | `201` + `DefinitionDetail`, or `400` with diagnostics, or `409` on filename collision |
| E4 | `PUT /SmarterPlaylist/Definitions/{fileName}` | M2 | S2 save | `200` + `DefinitionDetail`, `400` with diagnostics, `404`, or `409` on `SourceHash` mismatch |
| E5 | `POST /SmarterPlaylist/Validate` | M2 | S2 live validation | `Diagnostic[]`, no write, no side effects |
| E6 | `GET /SmarterPlaylist/Schema` | M1 | S2 renderer + helper text + S3 template | `{ Members: MemberDescriptor[], Orders: string[], MediaTypes: string[], DefaultMaxItems: 1000 }` |
| E7 | `DELETE /SmarterPlaylist/Definitions/{fileName}?deletePlaylist={bool}` | **M6** | S2 delete | `204` |
| E8 | `POST /SmarterPlaylist/Preview` | **M5** | Builder preview | `{ MatchCount, SampleTitles[], Truncated, ElapsedMs }` |
| E9 | `GET /SmarterPlaylist/FieldValues?member=&userId=&q=&limit=` | **M7** | Typeahead pickers | `string[]` distinct library values |
| E10 | `POST /SmarterPlaylist/Explain` | **N1**, out of scope | Per-item debug | — |

E1–E6 are the V1 cost. E7–E9 are the stated cost of M5–M7 and are listed so nobody is surprised later.

**E6 must be produced by reflection over `Operand`, never hand-written.** `Operand`'s property names and CLR types *are* the contract (`.planning/codebase/ARCHITECTURE.md`, "Reflection-bound contract"). A hand-maintained list would drift the moment N4 adds a property, reintroducing exactly the discoverability failure this page exists to fix.

`MemberDescriptor` shape:

```json
{
  "Name": "Directors",
  "ClrType": "System.Collections.ObjectModel.Collection`1[System.String]",
  "Kind": "TextList",
  "Operators": ["Contains", "MatchRegex", "NotMatchRegex"],
  "ValueControl": "libraryTypeahead",
  "DateRewritten": false,
  "Notes": "Contains matches a whole element exactly and is case-sensitive."
}
```

`Kind` ∈ `Text` | `TextEnum` | `TextList` | `Number` | `Date` | `Boolean`. Derivation rule, implemented once server-side:

| Condition on the `Operand` property | `Kind` |
|---|---|
| `typeof(bool)` | `Boolean` |
| `typeof(string)` and name is `MediaType` | `TextEnum` (values from `Enum.GetNames<MediaType>()`) |
| `typeof(string)` | `Text` |
| assignable to `IEnumerable<string>` | `TextList` |
| `typeof(double)` and name ends with `Date` or is `PremiereDate` | `Date` |
| numeric (`float`, `double`, `int`) | `Number` |

`DateRewritten` is `true` only for `PremiereDate` today — see defect D4.

### 4.2 Existing Jellyfin endpoints reused (no new backend)

| Purpose | Call |
|---|---|
| Target-user picker, and rendering user names | `ApiClient.getUsers()` |
| Next scheduled run, last task outcome | `ApiClient.getJSON(ApiClient.getUrl('ScheduledTasks'))`, find the entry whose `Key === 'RefreshAllPlaylists'` (*DERIVED* — `RefreshAllPlaylists.Key` returns `nameof(RefreshAllPlaylists)`) |
| "Run refresh now" | `ApiClient.ajax({ type: 'POST', url: ApiClient.getUrl('ScheduledTasks/Running/' + taskId) })` |

**NEEDS VERIFICATION:** the `ScheduledTasks` response field names (`LastExecutionResult.EndTimeUtc`, `LastExecutionResult.Status`, `Triggers[].IntervalTicks`, `State`) and the exact `ScheduledTasks/Running/{id}` verb on 10.11. **Fallback:** if the shape differs, the page header degrades to a static sentence naming the task and a link to *Dashboard → Scheduled Tasks*; it must not break the list.

### 4.3 Where per-definition status comes from

The deliverable asks this explicitly, because nothing in the current code records it.

**Decision: a DI-registered singleton `IRefreshStatusStore`, held in memory.**

```csharp
public sealed record RefreshStatus(
    string FileName,
    DateTime StartedUtc,
    DateTime CompletedUtc,
    RefreshOutcome Outcome,      // Succeeded | Failed | SkippedUnknownUser
    int? MatchedCount,           // before MaxItems
    int? AppliedCount,           // after MaxItems
    string? ErrorType,
    string? ErrorMessage);
```

Written by `RefreshAllPlaylists.RefreshPlaylistAsync` inside the per-definition `try`/`catch` that M3 introduces. Read by E1.

*Rejected alternatives, and why:*
- **Stamping status into the definition JSON.** Rejected: it makes user-authored files carry machine state and rewrites them every 30 minutes, which is a worse version of the reflow complaint already logged (`.planning/codebase/CONCERNS.md`, "Hand-authored JSON is reflowed to minified JSON") and would destroy git diffs for the Automator segment.
- **Plugin configuration XML (`Plugin.Instance.Configuration` + `SaveConfiguration()`).** Rejected: configuration is for settings; a write per definition per run is the wrong lifecycle for a 30-minute loop.
- **Sidecar status files under `SmarterPlaylists/.status/`.** Rejected for V1: a second on-disk format and extra I/O for a value that is stale within 30 minutes anyway. This is the upgrade path if users complain about the restart gap; do not build it up front.

*Accepted cost:* after a server restart, last-refresh status is unknown until the next task run. The UI has an explicit state for this (Section 6.3, `NeverRun`). This is acceptable because the **validation** half of M1's value — "your rule is broken" — is computed on demand by E1 and is always available, restart or not.

**Item counts are two different numbers and the UI must not conflate them:**

| Number | Source | Freshness |
|---|---|---|
| `PlaylistItemCount` | Live, computed in E1: resolve the user via `IUserManager.GetUserByName`, `IPlaylistManager.GetPlaylists(user.Id)` matched on the undashed id, then `playlist.GetChildren(user, false, query).Count` (*DERIVED* — mirrors `RefreshAllPlaylists.FindPlaylists` and its existing `InternalItemsQuery`) | Now |
| `MatchedCount` / `AppliedCount` | `IRefreshStatusStore` | Last run |

E1 also returns `PlaylistState` ∈ `NotCreated` (definition has no `Id`) | `Missing` (has an `Id` but no playlist resolves) | `Ok`, so the UI never has to infer from a null count.

**Backend change required:** `SmarterPlaylist.FilterPlaylistItems` currently returns only the capped id sequence, so the pre-cap count is unrecoverable. It must return both — e.g. change the return to a `FilterResult(IReadOnlyList<Guid> Ids, int MatchedCount)`. Without this, the "100 of 342" cell in Section 6.2 cannot be built and `MaxItems` truncation stays invisible — the exact silent-no-op class that already shipped once (`.planning/codebase/CONCERNS.md`, "`MaxItems` was a silent no-op").

### 4.4 Backend defects this UI exposes — all must be fixed for V1

These are not UI work, but the UI is incorrect or dishonest without them.

| ID | Defect | Why the UI needs it |
|---|---|---|
| D1 | **M3 blast radius.** Anything thrown inside `RefreshPlaylistAsync` propagates out of `ExecuteAsync` and aborts the run (`ARCHITECTURE.md`, "Error Handling — Gap"). | The `try`/`catch` that fixes it *is* the capture point for `IRefreshStatusStore`. Without it there is no per-definition error to render, and the page would claim other playlists refreshed when they did not. |
| D2 | **`Album` and `FolderPath` can be null.** `OperandFactory` assigns `baseItem.Album` and `baseItem.ContainingFolderPath` directly; both are nullable, while `Operand.Album` / `Operand.FolderPath` are non-nullable `string`. Any rule using those members throws `NullReferenceException` on the first item with a null value. | The UI offers those members in a dropdown (M4) and lists them as valid today. Fix: `?? string.Empty` at both assignment sites. Until fixed, the validator emits a Warning on rules touching them (Section 8.3, W4). |
| D3 | **Store and filesystem are constructed, not injected.** `RefreshAllPlaylists` news up `SmarterPlaylistStore`/`SmarterPlaylistFileSystem` (`CONCERNS.md`). | The controller and the task must share one instance and one `BasePath`. Register `ISmarterPlaylistStore`, `ISmarterPlaylistFileSystem`, and `IRefreshStatusStore` via an `IPluginServiceRegistrator`. |
| D4 | **`Engine.FixRules` rewrites only `PremiereDate`.** `DateCreated`, `DateLastRefreshed`, `DateLastSaved`, `DateModified` are `double` Unix seconds with no rewriting, so a human date literal on those members throws `FormatException` from `Convert.ChangeType` at refresh. This is undocumented — the README's table lumps all five under "Number". | The date picker must produce a value that works. Fix: in `FixRules`, apply the rewrite to all five members, but **only when `TargetValue` does not already parse as a number**, so existing files storing raw Unix seconds keep working. Then set `DateRewritten: true` for all five in E6. Until fixed, the validator errors on a non-numeric target for those four members (Section 8.2, E10). |
| D5 | **`SmarterPlaylistFileSystem.GetSmarterPlaylistFilePath` calls `.First()`** and throws `InvalidOperationException` when the file is absent. | E2/E4/E7 must return `404`, not `500`. |
| D6 | **`FileName` becomes a path segment on an HTTP-reachable surface.** `GetSmarterPlaylistPath` does `Path.Combine(BasePath, $"{playlistId}.json")` with no sanitisation. This was previously reachable only by someone who already had filesystem access; it is now reachable over HTTP. | Every endpoint taking `{fileName}` must reject anything not matching `^[A-Za-z0-9._-]{1,64}$`, and must additionally reject `.` and `..`, before touching the filesystem. This is a **new** security requirement introduced by this page and must not be skipped. |
| D7 | **`SaveAsync` minifies.** | Out of scope to fix on disk (N6). But **E2 must return the JSON pretty-printed for display** regardless of on-disk format, otherwise the Advanced editor opens on a single unreadable line. See Section 14. |

---

## 5. Information architecture

One page, one route. No client-side routing, no modal dialogs.

```
Dashboard → Plugins → SmarterPlaylist
│
├─ H  Page header
│     Title, task status sentence, "Run refresh now", link to Scheduled Tasks
│
├─ S1 Definitions list                       ← default and only top-level view
│     ├─ Row (collapsed)                     ← one per definition
│     └─ Row (expanded) → S2 detail panel    ← inline, in-flow, below the row
│                          ├─ Tab: Rules     ← read-only in V1, the builder in M4
│                          └─ Tab: Advanced (JSON)  ← the V1 editing surface
│
└─ S3 New definition                         ← inline panel above the table
```

**Decision: the detail view is an inline expanding panel, not a modal dialog.**

*Rejected alternative: a modal dialog.* Jellyfin's dialog helper (`components/dialogHelper`) is an ES module inside the app's bundle and is not verifiably reachable from a plain embedded config page. Building a modal by hand means hand-rolling a focus trap, scroll lock, and backdrop — three chances to break keyboard accessibility inside someone else's shell. An inline panel needs none of that, keeps the row visible as context while editing, and behaves better on a tablet in portrait. The cost is that only one definition can be open at a time (enforced: opening a row closes any other open row, after checking for unsaved changes).

---

## 6. S1 — Definitions list

### 6.1 Header (H)

```
SmarterPlaylist                                    [ Help ↗ ]
────────────────────────────────────────────────────────────
Playlists are rebuilt by the "Refresh all SmarterPlaylists"
scheduled task. Next run: in 12 minutes (17:30).
Definitions folder: /config/data/SmarterPlaylists

[ Run refresh now ]   [ Open Scheduled Tasks ↗ ]
```

- Heading: `h2.sectionTitle` inside `.sectionTitleContainer.flex.align-items-center`, with the Help link as `<a is="emby-button" class="raised button-alt headerHelpButton" target="_blank" href="https://github.com/poindexter12/jellyfin-plugin-smarterplaylist">Help</a>`.
- The task sentence and next-run come from `GET /ScheduledTasks` (Section 4.2). If that call fails or the shape is unrecognised, render the sentence without the "Next run" clause. Never block the list on it.
- Definitions folder path comes from `BasePath` on E1. It is shown always, not only in the empty state — Docker and NAS operators need it to hand-place or back up files, and the Automator segment needs it permanently.
- **"Run refresh now"** is `<button is="emby-button" class="raised button-alt">`. On click: disable, label → "Refreshing…", POST the task, then poll `GET /ScheduledTasks` every 2s for up to 5 minutes until the task's state is no longer running, then re-fetch E1 and re-render. On timeout, stop polling, re-enable, and toast "Refresh is still running. Reload this page to see results." Never leave the button in a permanent disabled state.
- If `ScheduledTasks` is unavailable, hide the button rather than showing one that cannot work.

### 6.2 Table columns — exact specification

`<table class="detailTable">` with `<caption class="clipForScreenReader">Smart playlist definitions</caption>` and `<th scope="col" class="detailTableHeaderCell">` headers.

| # | Header | Width intent | Cell contents |
|---|---|---|---|
| 1 | Status | 9rem, fixed | Material icon (`aria-hidden="true"`) + a text label. Never colour alone. See 6.3 for the value set and precedence. |
| 2 | Name | flexible, min 14rem | Line 1: `Name` from the definition, weight 500. If `Name` is empty, `(unnamed)` in `--jf-palette-text-secondary`. Line 2: `{FileName}.json` in `--jf-palette-text-secondary` at `0.9em`. The whole cell is the row's disclosure button (Section 11). |
| 3 | User | 10rem | The definition's `User` string, verbatim. If E1's diagnostics contain `UnknownUser` for this definition, append an `error` icon and, on the same line, `— no such user` in `--jf-palette-text-primary`. **The unknown-user determination is made server-side by E1** using `IUserManager.GetUserByName`, not by the client comparing against `getUsers()`, because the server's name-matching semantics are authoritative and were not verified to be case-sensitive or not. |
| 4 | Rules | 11rem | `{n} rules in {m} groups`. When `m > 1`, append ` · any group` — this is the only place OR-ness is visible in the collapsed row and it is not optional. Singular forms: `1 rule in 1 group`. When `m == 0`: `No rules` in `--jf-palette-text-secondary`. `title` attribute carries a one-line summary of group 1. |
| 5 | Items | 9rem | `PlaylistState == Ok` → `PlaylistItemCount`. If the last run's `MatchedCount > AppliedCount`, render `{AppliedCount} of {MatchedCount}` with `title="{MatchedCount} items matched; capped to {MaxItems} by Max items"`. `PlaylistState == NotCreated` → `—` with `title="The Jellyfin playlist has not been created yet. It is created on the first successful refresh."`. `PlaylistState == Missing` → `Playlist missing` in `--jf-palette-text-primary` with an `error` icon. |
| 6 | Last refresh | 11rem | Relative time (`12 minutes ago`), absolute local time in `title`. Prefix `Failed ` when `Outcome == Failed`. `Outcome == SkippedUnknownUser` → `Skipped`. No status record → `Unknown since restart` in `--jf-palette-text-secondary`. |
| 7 | Actions | 7rem, right-aligned | Icon buttons, in order: **Edit** (`edit`), **Validate** (`refresh`). M6 adds **Delete** (`delete`) at the end. M5 adds **Preview** (`visibility`) before Edit. Each is `<button is="paper-icon-button-light">` with `title` and a `<span class="clipForScreenReader">` label that includes the definition name, so accessible names are unique per row. |

Sort order: definitions are listed by `Name` (case-insensitive, invariant), with unnamed/unreadable definitions first so broken things are seen. No user-controlled sorting in V1.

### 6.3 Status values and precedence

Evaluated top-down; the first match wins. Each row has exactly one status.

| Order | Status | Icon | Label | Condition |
|---|---|---|---|---|
| 1 | Unreadable | `error` | `Unreadable` | The file is not parseable JSON, or does not deserialise to `SmarterPlaylistDto` |
| 2 | Invalid | `error` | `1 error` / `{n} errors` | One or more blocking diagnostics (Section 8.2) |
| 3 | Failed | `error` | `Refresh failed` | `IRefreshStatusStore.Outcome == Failed` |
| 4 | Skipped | `warning` | `Skipped` | `Outcome == SkippedUnknownUser` (and not already caught by rule 2) |
| 5 | Warnings | `warning` | `1 warning` / `{n} warnings` | One or more non-blocking diagnostics (Section 8.3) |
| 6 | Never run | `schedule` | `Not yet refreshed` | Valid, but no status record exists |
| 7 | OK | `check_circle` | `OK` | Everything else |

Icon colour: statuses 1–3 use `color: var(--jf-palette-error-main, #c62828)` on the **icon only**. Statuses 4–5 use `--jf-palette-text-secondary`. Statuses 6–7 use inherited colour. There is no green anywhere — Jellyfin ships no success token (Section 3.3), and inventing one would be the first crack in "looks native".

### 6.4 Page states

| State | Trigger | Rendering |
|---|---|---|
| **Loading** | Initial `pageshow`, and any full refetch of E1 | `Dashboard.showLoadingMsg()`, plus the table region replaced by `<p role="status">Loading playlist definitions…</p>`. No skeleton rows — Jellyfin has no skeleton primitive and hand-rolling one would not look native. |
| **Empty** | E1 returns `Definitions: []` | A `.verticalSection` containing: `h3` "No smart playlists yet"; a paragraph — "Smart playlists are defined as JSON files in `{BasePath}`. Create one here, or drop a file into that folder and it will appear on this page."; a primary button `<button is="emby-button" class="raised button-submit block">Create your first playlist</button>` opening S3. The folder path is selectable text, not inside a button. |
| **Populated** | E1 returns ≥1 definition | The table, plus a `<p aria-live="polite">` summary line above it: `{n} definitions · {e} with errors · {w} with warnings`. Clauses with a zero count are omitted. |
| **Per-row error** | The row's status is 1–5 | The status cell shows the chip; the expanded panel shows the diagnostics block (Section 7.2). The row itself gets `border-left: 3px solid var(--jf-palette-error-main, #c62828)` for statuses 1–3 — decoration only, never the sole signal. |
| **Page-level failure** | E1 returns non-2xx, or the request rejects | The table region is replaced by a block: `h3` "Couldn't load playlist definitions"; a line with the HTTP status and, when present, the `X-Application-Error-Code` response header; the sentence "The plugin's API may not be registered. Check the Jellyfin server log for `SmarterPlaylist`."; a `<button is="emby-button" class="raised button-alt">Retry</button>`. **Do not** call `Dashboard.processErrorResponse` here — it hides loading and pops a modal, leaving an empty page behind it. Reserve modals for actions the user just took. |
| **Partial failure** | E1 succeeds but a per-row live lookup (playlist count) threw server-side | That row's Items cell shows `—` with `title="Could not read the playlist"`. The rest of the page is unaffected. E1 must never fail wholesale because one playlist lookup failed. |

---

## 7. S2 — Definition detail panel

Rendered inline, in document flow, immediately after the row's `<tr>` as a full-width `<tr><td colspan="7">` container. This keeps table semantics intact (a `<div>` between rows would not be valid table markup).

### 7.1 Layout

```
┌──────────────────────────────────────────────────────────────┐
│ CGP Grey                                          [ Close ✕ ] │
│ cgp_grey.json · for user "rob" · sorted Release Date Asc      │
│ ───────────────────────────────────────────────────────────── │
│ ( Rules )  ( Advanced (JSON) )                                │  ← tabs
│                                                               │
│ [ tab body ]                                                  │
│                                                               │
│ ───────────────────────────────────────────────────────────── │
│ [ Save ]  [ Validate ]  [ Revert ]        [ Delete ] (M6)      │
└──────────────────────────────────────────────────────────────┘
```

Tabs: two `<button role="tab" aria-selected aria-controls>` inside a `<div role="tablist">`. Do not use `emby-tabs` — *NEEDS VERIFICATION* whether it initialises correctly outside the app's module graph, and hand-rolled tab buttons are two ARIA attributes. Left/Right arrow keys move between tabs; the tab panel is `role="tabpanel"` with `tabindex="0"`.

### 7.2 Tab: Rules (V1 — read-only)

This tab is read-only in V1 and becomes the M4 builder without moving. Rendering it in V1 is not optional: it is the only place the OR/AND structure is expressed faithfully, and it proves out the E6 schema that M4 depends on.

```
Match items in ANY of these groups:

  Group 1  — all of:
    Directors   contains (exact element)   "CGP Grey"
    Is played   is                          False

  OR

  Group 2  — all of:
    Directors   contains (exact element)   "Nerdwriter1"
    Is played   is                          False

Then sort by Release Date Ascending and keep the first 100.
```

Rules for this rendering:
- The words **ANY** and **all of** are rendered in weight 600. They carry the semantics and must be the most salient text in the block.
- When there is exactly one group, the heading collapses to `Match items where all of:` and the group label is omitted. Do not show a one-branch OR.
- When there are zero groups, render `This definition has no rule groups, so it matches no items.` as a blocking diagnostic, not as a rule rendering.
- When a group has zero rules, render `This group has no rules, so it matches every item in the library.` in that group's slot, as a blocking diagnostic.
- Operators are rendered with a plain-language gloss taken from E6's `MemberDescriptor`, never as the raw enum name alone. Glosses: `Contains` on a list → `contains (exact element)`; `Contains` on text → `contains text`; `Equal` on bool → `is`; `MatchRegex` → `matches regex`; `NotMatchRegex` → `does not match regex`; `GreaterThanOrEqual` → `is at least`; and so on. The raw operator name is in the `title` attribute for anyone who is editing JSON.
- Date members render the stored value **and** its human form: `Premiere date is at least 2020-07-01 (1593561600)`.
- The trailing sentence always states the sort order and the effective `MaxItems`, including when `MaxItems` is 0: `…and keep the first 1000 (default).` `MaxItems` truncation is invisible in Jellyfin's playlist UI and has already shipped as a silent no-op once; it gets a sentence.

Diagnostics block, rendered above the rule rendering when any diagnostic exists:

```
┌ ⚠ 2 problems ───────────────────────────────────────────────┐
│ Group 2 › Rule 1 — Operator                                  │
│ "GreaterThan" is not valid for Directors (list of text).      │
│ Valid operators: Contains, MatchRegex, NotMatchRegex.         │
│                                                              │
│ Definition — User                                            │
│ There is no Jellyfin user named "rob".                        │
└──────────────────────────────────────────────────────────────┘
```

- Each diagnostic is a `<li>`. The location line (`Group 2 › Rule 1 — Operator`) is weight 600; the message is normal weight in `--jf-palette-text-primary`.
- Diagnostics without a rule location render under the heading `Definition`.
- When the diagnostic carries a `Suggestion`, append a final line: `Did you mean "PremiereDate"?`
- Runtime failures recorded by `IRefreshStatusStore` render in their own sub-block headed `Last refresh failed at {absolute local time}`, with the exception type and message in a `<pre style="white-space: pre-wrap">`. This is the exact string that today only exists in the server log; it is reproduced verbatim, not paraphrased.
- JSON parse failures use `JsonException.LineNumber` and `BytePositionInLine` to render `Line 14, position 5: '}' expected.` and, when the Advanced tab is opened, the editor scrolls to and selects that line.

### 7.3 Tab: Advanced (JSON) — the V1 editing surface

- A `<textarea is="emby-textarea">` with `spellcheck="false"`, `autocapitalize="off"`, `autocorrect="off"`, `wrap="off"`, monospace via `font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace`, `min-height: 24em`, resizable vertically.
- Seeded from E2's pretty-printed JSON (see defect D7).
- `aria-describedby` points at a `.fieldDescription`: `Edits are validated against the plugin's rule engine before they are saved. Property names and operators are case-sensitive.`
- **No syntax highlighting, no code editor library.** That would mean a CDN or a bundled dependency, both forbidden by constraint 2.
- Live validation: debounced 600ms after typing stops, and immediately on blur → `POST /Validate`. Results render into the diagnostics block on the Rules tab **and** into a compact `<p role="status">` under the textarea: `Valid` / `{n} errors, {m} warnings`. Never validate per keystroke.
- Tab key inserts a literal tab character rather than moving focus **only when** the user has pressed Tab twice in quick succession — no. Simpler and accessible: **Tab always moves focus.** Indentation is the author's problem; trapping Tab in a textarea is a well-known keyboard trap. Stated explicitly so nobody "helpfully" adds it.

**Judgement call, stated because it is contestable.** UX-RESEARCH §3 says to avoid a free-text DSL as the *primary* interface, and in V1 this textarea is the primary editing interface. It is shipped anyway because: (a) the README itself identifies "a page that simply lists and edits the JSON" as the correct cheapest first step; (b) it is strictly better than the status quo, which is filesystem access inside a container; (c) M2's synchronous validation removes the property/operator discoverability failure that made JSON authoring hostile; and (d) the Rules tab already occupies the position the M4 builder will take, so the builder replaces the *default* tab rather than being bolted on. *Rejected alternative: shipping M1 read-only with no editing at all.* Rejected because M2 ("validate on save") then has no save to validate, and the milestone would ship half-implemented.

### 7.4 Footer actions

| Button | Element | Enabled when | Behaviour |
|---|---|---|---|
| **Save** | `<button is="emby-button" class="raised button-submit">` | The document is dirty **and** the last validation returned zero blocking errors | `PUT /Definitions/{fileName}` with the raw text and `SourceHash`. On `200`: toast via `Dashboard.alert('Playlist definition saved.')`, refetch E1, re-render the row, keep the panel open, move focus to the panel heading. On `400`: render diagnostics into a `role="alert"` container and move focus to it. On `409`: see below. Do **not** call `Dashboard.processPluginConfigurationUpdateResult()` — these are not plugin configuration values and its localised "Settings saved" string would be misleading. |
| **Validate** | `raised button-alt` | Always | `POST /Validate`. Renders diagnostics into a `role="alert"` container. Used when the user wants a check without a debounce wait. |
| **Revert** | `raised button-alt` | Dirty | `Dashboard.confirm('Discard your unsaved changes to this definition?', 'Discard changes', function (ok) { … })`. On confirm, re-seed from the last E2 response. |
| **Delete** (M6) | `raised button-alt`, right-aligned, error-tinted border only | Always | Section 13.3 |

**Concurrent-edit conflict (409).** E2 returns `SourceHash` — a hash of the file bytes as read. E4 requires it and returns `409` when the file changed underneath. UI: a `role="alert"` block — *"This file was changed on disk since you opened it. Someone else, or a hand edit, may have modified it."* — with two buttons, `Reload from disk` (discards the user's edits after a `Dashboard.confirm`) and `Overwrite anyway` (re-PUTs with the current hash). This exists because the Automator and Tinkerer segments explicitly keep editing files by hand, and silently clobbering their work would be the worst possible first impression for the page.

**Unsaved-changes guard.** Attempting to collapse the panel, open another row, or leave the page while dirty triggers `Dashboard.confirm('You have unsaved changes to "{Name}". Discard them?', 'Unsaved changes', …)`. There is no `beforeunload` handler — it is unreliable inside the dashboard's SPA routing and produces a browser-chrome dialog that does not look native.

---

## 8. Validation model

### 8.1 When validation fires

| Moment | Mechanism | Blocking? |
|---|---|---|
| Page load | Server-side, inside E1, for every definition on disk | Informational — populates row status |
| Row expand | Server-side, inside E2 | Informational |
| Typing in the Advanced editor | `POST /Validate`, debounced 600ms | Informational; gates the Save button's enabled state |
| Blur of the Advanced editor | `POST /Validate`, immediate | Same |
| Explicit **Validate** press | `POST /Validate`, immediate | Same, plus focus moves to the results |
| **Save** press | Server re-validates inside E3/E4 and refuses the write | **Authoritative** |

**The client never performs validation itself.** Every rule below is implemented once, server-side, in a `DefinitionValidator`. The client's only job is rendering `Diagnostic[]` and enabling or disabling Save. This is deliberate: operator legality is emergent from `Operand`'s CLR types and target-value coercion is `Convert.ChangeType` semantics — reimplementing either in JavaScript guarantees the two will disagree, and the disagreement will present as "the UI said it was fine and then it broke", which is precisely the failure this page exists to eliminate.

Corollary: the validator must determine target-value legality by **actually attempting the coercion the engine will attempt** (`Convert.ChangeType` to the member's CLR type, `DateTime.Parse` for rewritten date members, `new Regex(...)` for regex operators) inside a `try`/`catch`, not by pattern-matching strings.

### 8.2 Blocking errors — Save is refused

| ID | Condition | Message |
|---|---|---|
| E01 | File is not parseable JSON | `Line {n}, position {p}: {message}` |
| E02 | `Name` is empty or whitespace | `Give the playlist a name. This is what appears in Jellyfin.` |
| E03 | `FileName` empty, or fails `^[A-Za-z0-9._-]{1,64}$` | `File name may only contain letters, numbers, dot, dash and underscore, up to 64 characters.` |
| E04 | `FileName` collides with an existing definition (create only) | `A definition named "{FileName}.json" already exists.` |
| E05 | `User` is empty | `Choose the Jellyfin user this playlist is for.` |
| E06 | `User` does not resolve via `IUserManager.GetUserByName` | `There is no Jellyfin user named "{User}".` |
| E07 | `ExpressionSets` is empty | `This definition has no rule groups, so it matches no items.` |
| E08 | Some `ExpressionSet.Expressions` is empty | `Group {n} has no rules, so it matches every item in the library.` |
| E09 | `MemberName` is not a public property of `Operand` | `"{MemberName}" is not a filterable property.` + `Suggestion` when a case-insensitive or edit-distance-1 match exists |
| E10 | `Operator` is not in E6's legal list for that member's `Kind` | `"{Operator}" is not valid for {MemberName} ({kind gloss}). Valid operators: {list}.` |
| E11 | `TargetValue` does not coerce to the member's CLR type | `"{TargetValue}" is not a valid {kind gloss} value for {MemberName}.` |
| E12 | `TargetValue` for `MatchRegex`/`NotMatchRegex` is not a valid `Regex` | `Not a valid regular expression: {message}` |
| E13 | `MaxItems` < 0 | `Max items cannot be negative. Use 0 for the default of 1000.` |
| E14 | `Order.Name` is not a recognised order | `"{Order.Name}" is not a known sort order. Valid: {list}.` |

Note on **E14**: `SmarterPlaylist`'s constructor silently falls back to `NoOrder` for an unrecognised name. That is exactly the "plausible wrong answer with no error" pattern flagged in `ARCHITECTURE.md`. The engine's fallback stays (hand-edited files must keep working); the UI refuses to *write* one.

Note on **E08**: making an empty group a blocking error rather than a warning is a judgement call. *Rejected alternative: a warning.* Rejected because the consequence is an accidental playlist of the user's entire library up to `MaxItems`, the intent is almost never deliberate, and there is no cheap way to undo it once the scheduled task has run. A user who genuinely wants everything can write a rule that always passes.

### 8.3 Warnings — Save proceeds, warnings are shown

| ID | Condition | Message |
|---|---|---|
| W01 | `MaxItems` is 0 | `Max items is 0, so the default of 1000 applies.` |
| W02 | `Equal` or `NotEqual` on `CommunityRating` / `CriticRating` | `Exact equality on a rating rarely matches. Consider "is at least".` |
| W03 | `Contains` on a list member with a value not present anywhere in the target user's library | `No {member} in this library is exactly "{value}". Contains needs a whole exact element — use "matches regex" for partial matches.` — **V1-optional**, requires E9; ship it with M7 if E9 is not built for V1. |
| W04 | Any rule on `Album` or `FolderPath` **while defect D2 is unfixed** | `Rules on {member} can fail during refresh when an item has no value. See the plugin's known issues.` — remove this warning when D2 lands. |
| W05 | `MediaType` value not in `Enum.GetNames<MediaType>()` | `"{value}" is not a known media type. Known values: {list}.` |

Warnings are shown in the diagnostics block with a `warning` icon, and the save confirmation names them: `Saved with 2 warnings.`

### 8.4 Case sensitivity — must be stated in the UI

*DERIVED* from `Engine.BuildExpr`: `Enum.TryParse(r.Operator, out ExpressionType)` is case-sensitive by default, member lookup is `typeof(T).GetProperty(name)` (case-sensitive), and `string.Contains(string)` / `Collection<string>.Contains` are ordinal. Every free-text value control carries the `.fieldDescription` sentence: `Matching is case-sensitive.` M4's dropdowns make this moot for member and operator names but not for target values.

---

## 9. Rule builder — control by property type (M4)

Specified now because E6 must emit it from V1 and the V1 read-only renderer already consumes it. Derived from the actual `Operand` CLR types, not from the README table.

| `Operand` property | CLR type | `Kind` | Operators offered | Value control | Serialised `TargetValue` |
|---|---|---|---|---|---|
| `Name` | `string` | `Text` | `Equal`, `NotEqual`, `Equals`, `Contains`, `StartsWith`, `EndsWith`, `MatchRegex`, `NotMatchRegex` | `<input is="emby-input" type="text">` | verbatim |
| `Album` | `string` | `Text` | as above | text | verbatim (see W04) |
| `FolderPath` | `string` | `Text` | as above | text | verbatim (see W04) |
| `MediaType` | `string` | `TextEnum` | `Equal`, `NotEqual`, `Equals` | `<select is="emby-select">` populated from E6's `MediaTypes` | the enum name verbatim |
| `Actors`, `Composers`, `Directors`, `Genres`, `GuestStars`, `Producers`, `Studios`, `Writers` | `Collection<string>` | `TextList` | `Contains`, `MatchRegex`, `NotMatchRegex` | **`Contains`** → typeahead over E9's library values (M7), free text until then. **regex** → text input with the hint `Matches if any single {member} matches.` | verbatim |
| `CommunityRating`, `CriticRating` | `float` | `Number` | `Equal`, `NotEqual`, `GreaterThan`, `GreaterThanOrEqual`, `LessThan`, `LessThanOrEqual` | `<input is="emby-input" type="number" step="0.1" min="0" max="10">` | **invariant** decimal — always `Number.prototype.toString()`, never `toLocaleString`; a comma decimal separator throws in `Convert.ChangeType` |
| `PremiereDate` | `double` | `Date` | the six numeric operators | `<input is="emby-input" type="date">` | `YYYY-MM-DD`; `Engine.FixRules` rewrites it to Unix seconds |
| `DateCreated`, `DateLastRefreshed`, `DateLastSaved`, `DateModified` | `double` | `Date` | the six numeric operators | `<input is="emby-input" type="date">` | **`YYYY-MM-DD` once defect D4 is fixed. Until then, the UI must emit Unix seconds and the builder must not offer a date picker for these four**, because `FixRules` only rewrites `PremiereDate` and a date literal throws `FormatException` at refresh |
| `IsPlayed` | `bool` | `Boolean` | `Equal`, `NotEqual` | `<select is="emby-select">` with two options | exactly `True` or `False`. Never `0`/`1` — `Convert.ChangeType("1", typeof(bool))` throws |

The operator dropdown is populated from `MemberDescriptor.Operators` and is **rebuilt** when the property changes. If the previously selected operator is not in the new list, it is cleared and the operator select is focused, with the `.fieldDescription` reading `Choose an operator for {member}.` It is never silently coerced to another operator — UX-RESEARCH §3 explicitly warns against silent auto-correction.

Group management (M4): one group by default, no visible group chrome until a second group exists. `+ Add rule` inside a group. `+ Add another rule group (matches if this OR any other group matches)` as a secondary, lower-prominence action below the group. This maps one `ExpressionSet` to one group and matches the iTunes convention the research prescribes.

---

## 10. Forward compatibility — proving V1 does not paint M4–M7 into a corner

| Milestone | What it needs | What V1 already provides | Residual work |
|---|---|---|---|
| **M4** builder | Property/operator/type metadata; a place to live; a read/write round-trip | E6 emits the full descriptor set; the Rules tab is already the default tab and already renders the OR/AND model; E2/E4 already round-trip a whole definition | Replace the Rules tab body with editable rows; serialise the builder state to the same JSON E4 already accepts. The Advanced tab stays as the Tinkerer escape hatch, which is exactly the role §3 prescribes for it. |
| **M5** preview | An endpoint that filters without writing; a place to show a count | E8 is specified but not built. The detail panel footer has room; the panel is inline, so a preview result does not fight a modal for space | Build E8. Reuse `SmarterPlaylist.FilterPlaylistItems` against a cached per-user item set. Performance must be measured first per UX-RESEARCH §5 assumption 2; if slow, the count renders as `about {n}` with a sampled subset and an explicit `estimated` label. |
| **M6** delete | An endpoint; a decision on the orphaned Jellyfin playlist | The footer reserves the Delete slot; E1 already returns `PlaylistState` so the UI knows whether an orphan would exist | Build E7 and wire `ISmarterPlaylistStore.Delete` to its first ever caller. Confirmation copy in 13.3. |
| **M7** pickers | Distinct library values per member per user | E6 already marks list members with `ValueControl: "libraryTypeahead"`, so V1's renderer and M4's builder both already know which fields want a picker | Build E9. W03 activates at the same time. |

The one real coupling risk is E6. If V1 ships a hand-maintained property list instead of reflection over `Operand`, M4 and N4 both break. This is why Section 4.1 states it as a hard requirement rather than a preference.

---

## 11. Interaction rules

1. **Row disclosure.** The Name cell is a `<button aria-expanded="false" aria-controls="detail-{fileName}">` spanning the cell. Clicking anywhere else in the row does nothing — a whole-row click target on a table containing action buttons produces accidental expansions on touch.
2. **One panel at a time.** Expanding a row collapses any other expanded row, after the unsaved-changes guard (7.4).
3. **Escape** collapses the open panel, after the unsaved-changes guard, and returns focus to the disclosure button.
4. **No optimistic UI.** The row does not update until the server has confirmed the write and E1 has been refetched. Status, counts, and diagnostics are all server-derived; guessing them locally would recreate the plausible-wrong-answer pattern in a new layer.
5. **No autosave.** Ever. Saving writes a file that a 30-minute scheduled task consumes; an accidental autosave of a half-typed rule is a real playlist being rebuilt wrongly.
6. **Debounce is 600ms** for validation. Nothing else is debounced.
7. **Destructive actions** (Revert, Delete, Overwrite anyway) always route through `Dashboard.confirm`. Non-destructive successes use the toast form of `Dashboard.alert`.
8. **Every network call has a visible terminal state.** Success → toast or rendered result. Failure → a rendered error with the HTTP status. No call may end in silence.

---

## 12. Responsive behaviour

The Jellyfin dashboard is used on tablets, including in portrait. Breakpoints are on the page container.

| Range | Layout |
|---|---|
| **≥1000px** | All seven columns. |
| **640–999px** | Five columns. **Rules** folds into the Name cell as a third line (`3 rules in 2 groups · any group`). **Last refresh** folds into the Status cell as a second line. |
| **<640px** | Three columns: **Status**, **Name**, **Actions**. Name carries name, filename, user, rules summary, item count, and last refresh as stacked lines at `0.9em` in `--jf-palette-text-secondary`. |

*Rejected alternative: CSS-stacked "card" rows via `display: block` on `tr`/`td`.* Rejected because setting `display: block` on table elements strips their implicit ARIA roles in several browsers, silently breaking the table for screen-reader users on exactly the devices where the layout is most compressed.
*Rejected alternative: a horizontally scrolling table wrapper.* Rejected because horizontal scroll inside a vertically scrolling dashboard page is poor on touch and hides the Actions column, which is the column most likely to be wanted.

Additional requirements:
- All interactive targets are **≥44×44 CSS px**, enforced in the scoped stylesheet (`min-height: 2.75rem; min-width: 2.75rem`) rather than assumed from `emby-button`'s own sizing.
- The detail panel is single-column below 900px: tabs stack above the tab body, and the footer buttons wrap with `gap: 0.5rem` rather than shrinking.
- The Advanced textarea is `min-height: 24em` on desktop and `min-height: 14em` below 640px, so the on-screen keyboard does not leave a two-line editing slot.
- No horizontal scrolling of the page at any width down to 360px.
- Nothing depends on hover. Every `title` tooltip has its content also available in the expanded panel, because touch users never see a `title`.

---

## 13. Copy deck

Exact strings. Sentence case throughout, matching Jellyfin's dashboard convention. No exclamation marks. No emoji.

### 13.1 Primary calls to action

| Context | Label |
|---|---|
| Empty state primary | `Create your first playlist` |
| List header, create | `New smart playlist` |
| Detail footer, save | `Save` |
| Detail footer, validate | `Validate` |
| Detail footer, revert | `Revert` |
| Header, manual run | `Run refresh now` |
| Header, while running | `Refreshing…` |

### 13.2 Empty and never-run states

| Context | Copy |
|---|---|
| No definitions, heading | `No smart playlists yet` |
| No definitions, body | `Smart playlists are defined as JSON files in {BasePath}. Create one here, or drop a file into that folder and it will appear on this page.` |
| Row, never refreshed | `Not yet refreshed` |
| Row, never refreshed, tooltip | `This definition has not run since the server started. It runs with the next scheduled refresh.` |
| Playlist not created | `The Jellyfin playlist has not been created yet. It is created on the first successful refresh.` |

### 13.3 Destructive actions (M6)

There is exactly one destructive action in this surface: deleting a definition.

`Dashboard.confirm` is a plain message + title + callback, so the choice about the orphaned Jellyfin playlist cannot live inside it. Therefore: the Delete button reveals an inline confirmation block in the panel footer, not a modal.

```
Delete "CGP Grey"?
This removes cgp_grey.json. It cannot be undone.

( ) Also delete the Jellyfin playlist "CGP Grey" (100 items)
( ) Keep the Jellyfin playlist as a static list        ← default

[ Delete definition ]   [ Cancel ]
```

- The radio group is `<fieldset>` + `<legend class="clipForScreenReader">What should happen to the Jellyfin playlist?</legend>`, using `emby-radio`.
- **Keep** is the default. *Rejected alternative: defaulting to also-delete.* Rejected because the Jellyfin playlist may be shared, favourited, or in someone's queue, and the definition file is the only thing the user unambiguously asked to remove. Data loss is not the safe default.
- When `PlaylistState != Ok` the radio group is omitted entirely and the copy reads `This removes cgp_grey.json. No Jellyfin playlist is linked to it.`
- Focus moves to the confirmation heading when it appears; Escape cancels and returns focus to the Delete button.

### 13.4 Error copy principles

Every error string states the problem **and** the next action, and names the specific thing:

- Bad: `Invalid operator.`
- Good: `"GreaterThan" is not valid for Directors (list of text). Valid operators: Contains, MatchRegex, NotMatchRegex.`
- Bad: `Save failed.`
- Good: `Couldn't save cgp_grey.json — the server returned 500. Check the Jellyfin server log for SmarterPlaylist.`

Never say "an error occurred", never surface a bare stack trace as the primary message (the full exception belongs in the `<pre>` sub-block, labelled), and never blame the user.

---

## 14. What V1 must not preclude (out-of-scope items)

| Item | V1 obligation |
|---|---|
| **N1** per-item match debug | Diagnostics carry `ExpressionSetIndex` / `ExpressionIndex` from day one, so a per-rule pass/fail overlay can reuse the same location model and the same rendering component. |
| **N2** plain-language sentence | The Rules tab renderer already glosses operators into plain language. N2 is a presentation variant of the same data, not a new pipeline. |
| **N3** deeper nesting | The Rules renderer must treat "groups" as a list it iterates, not as exactly-two-levels hardcoded markup. No V1 code may assume `ExpressionSets.Count <= 2`. |
| **N4** new `Operand` properties / orders | E6 is reflection-derived (Section 4.1) and `Orders` is a server-supplied list, so a new property or order appears in the UI with zero front-end change. **This is the single most important forward-compatibility constraint in this spec.** |
| **N5** per-user self-service | Not achievable through a config page at all (constraint 4). V1 must nonetheless keep `User` as a first-class field on the definition and must never assume "the current admin" is the target user, so that a future non-config-page surface can reuse the same file format and the same endpoints. |
| **N6** pretty-printed JSON on disk | E2 must return pretty-printed JSON **for display** regardless of on-disk format (defect D7), so the Advanced editor is usable without N6. When N6 lands, E2's formatting step becomes a no-op — no UI change. |

---

## 15. Accessibility requirements

Non-negotiable. Each is testable.

**Structure and labelling**
- The definitions list is a real `<table>` with `<caption class="clipForScreenReader">`, `<thead>`, and `<th scope="col">`. No ARIA grid pattern — a static table needs none, and a half-implemented grid is worse than a table.
- Every control has an accessible name. Icon-only buttons pair a `title` with a `<span class="clipForScreenReader">` that **includes the definition name**, so names are unique across rows: `Edit CGP Grey`, `Delete CGP Grey`.
- All Material icons are `aria-hidden="true"`. They never carry meaning alone.
- Every input is associated with a label, via `emby-input`'s `label` attribute or an explicit `<label for>`. Placeholder text is never a substitute for a label.
- Helper text is linked with `aria-describedby`; a field with an error also gets `aria-invalid="true"` and its `aria-describedby` extended to include the error element's id.

**Keyboard**
- Every action reachable by pointer is reachable by keyboard in DOM order. No positive `tabindex` anywhere.
- Row disclosure is a `<button aria-expanded aria-controls>`, operable with Enter and Space.
- Tabs: Left/Right arrows move between tabs, Home/End jump to first/last, Enter/Space activates. The tab panel is `tabindex="0"`.
- Escape closes the open detail panel and the delete confirmation.
- **No keyboard traps.** Explicitly: Tab inside the Advanced textarea moves focus and does not insert a tab character (7.3).
- Visible focus indication is never removed. If a custom control needs one, use `outline: 2px solid var(--jf-palette-primary-main, #00a4dc); outline-offset: 2px`.

**Focus management**
- Expanding a row moves focus to the panel's `<h3 tabindex="-1">` heading.
- Collapsing a row returns focus to the disclosure button that opened it.
- After a save that produces errors, focus moves to the diagnostics container.
- After a successful save, focus stays where it is; the toast announces the result. Never yank focus on success.
- Revealing the delete confirmation moves focus to its heading; cancelling returns focus to the Delete button.

**Announcement**
- The list summary line (`12 definitions · 2 with errors`) is `aria-live="polite"`. This is the only thing announced on page load — per-row diagnostics are **not** live regions at load time, which would flood a screen reader with a dozen alerts.
- Diagnostics rendered **in response to a user action** (Validate, Save, conflict) are in a `role="alert"` container that is emptied and repopulated so the change is announced.
- The "Run refresh now" progress is announced through a `role="status"` region: `Refreshing…` then `Refresh finished.`

**Colour and contrast**
- No information is conveyed by colour alone. Every status is icon + text (Section 6.3).
- `--jf-palette-error-main` measures ≈2.9:1 against the dark theme paper background and is therefore restricted to decorative borders and icon fills; error message text uses `--jf-palette-text-primary` (Section 3.3).
- Secondary text uses `--jf-palette-text-secondary`, which the themes define as `rgba(0,0,0,0.87)` / theme default rather than a low-contrast grey. Do not add opacity on top of it.
- The page must be usable at 200% browser zoom and at a 320px CSS viewport width without horizontal scrolling.

**Motion**
- The panel expand/collapse is the only animation and must be wrapped in `@media (prefers-reduced-motion: no-preference)`. Under reduced motion it is an instant show/hide.

---

## 16. Non-goals

Stated so they are not re-litigated during implementation.

1. **A separate design system.** No CSS framework, no CDN, no web fonts, no icon set beyond Jellyfin's bundled Material Icons, no bundler, no build step for the page.
2. **A modal-based UX.** No dialogs beyond `Dashboard.alert` and `Dashboard.confirm`.
3. **A code editor.** The Advanced tab is a plain textarea. No syntax highlighting, no linting gutter, no bracket matching.
4. **Client-side validation logic.** The client renders diagnostics; it never derives them (Section 8.1).
5. **Per-user self-service.** Structurally unreachable from a config page (constraint 4).
6. **Nested boolean groups beyond the engine's two-level DNF.** N3, and gated on evidence.
7. **Editing the scheduled task's interval.** That is Jellyfin's Scheduled Tasks page; the header links to it rather than duplicating it.
8. **Bulk operations.** No multi-select, no bulk delete, no bulk enable/disable. There is no enabled/disabled concept in the data model and inventing one is a data-model change, not a UI feature.
9. **Import/export.** The files are already on disk at a path the page displays.
10. **Real-time updates.** No polling of the definitions list except during an explicitly triggered refresh. No websockets.
11. **Localisation.** Plugin config pages can use `${Token}` globalisation keys (*NEEDS VERIFICATION* of the mechanism for plugin-supplied strings), but V1 ships English literals. Strings are centralised in one object at the top of the script block so this is a mechanical change later.

---

## 17. Judgement calls and rejected alternatives — index

| § | Decision | Rejected alternative | Reason |
|---|---|---|---|
| 4.3 | In-memory `IRefreshStatusStore` | Status in the definition JSON | Machine state in user files; destroys diffs; reintroduces the reflow complaint |
| 4.3 | In-memory `IRefreshStatusStore` | Plugin configuration XML | Wrong lifecycle for a 30-minute write loop |
| 4.3 | In-memory `IRefreshStatusStore` | Sidecar status files | Second on-disk format for a value stale within 30 minutes; keep as the upgrade path |
| 5 | Inline expanding detail panel | Modal dialog | `dialogHelper` is not verifiably reachable from an embedded page; hand-rolled modals break keyboard access |
| 6.3 | No green "success" colour | Invent a success token | Jellyfin ships none; inventing one is the first crack in "looks native" |
| 7.2 | Read-only Rules tab in V1 | Ship M1 with no rule rendering | OR/AND semantics would be invisible, and E6 would go unproven before M4 depends on it |
| 7.3 | Advanced JSON textarea as V1's editor | Read-only V1 | M2 would have no save path; the milestone would ship half-done |
| 7.3 | Tab moves focus in the textarea | Tab inserts indentation | Classic keyboard trap |
| 7.4 | Custom "saved" toast | `Dashboard.processPluginConfigurationUpdateResult()` | These are not plugin configuration values; its "Settings saved" string would mislead |
| 8.1 | Server-only validation | Mirror the rules in JS | The two would diverge, producing "the UI said it was fine and then it broke" |
| 8.2 E08 | Empty rule group is a blocking error | A warning | Silently matches the entire library up to `MaxItems`, with no cheap undo |
| 8.2 E14 | Refuse to write an unknown sort order | Match the engine's silent `NoOrder` fallback | The fallback is the plausible-wrong-answer pattern; keep it for old files, refuse to create new ones |
| 11.4 | No optimistic UI | Update the row immediately | Status and counts are server-derived; guessing them recreates the silent-wrong-answer pattern in a new layer |
| 12 | Hide columns below 640px | CSS-stacked cards | `display: block` on table elements strips implicit ARIA roles |
| 12 | Hide columns below 640px | Horizontal scroll wrapper | Poor on touch; hides the Actions column |
| 13.3 | "Keep the Jellyfin playlist" is the delete default | Default to also-deleting | The playlist may be shared or queued; data loss is not a safe default |

---

## 18. Open questions for the maintainer

1. **Does the UI become the sole source of truth, or does hand-edited JSON stay a first-class parallel path?** This spec assumes **parallel**, which is why it specifies the `SourceHash` conflict flow (7.4), keeps the definitions folder path permanently visible (6.1), and treats a file appearing on disk as a first-class way to create a definition. If the answer is "UI is authoritative", the conflict flow can be simplified but the Automator segment loses its workflow.
2. **Is the in-memory status store's restart gap acceptable**, or should sidecar status files ship in V1? This spec says acceptable; Section 4.3 documents the upgrade path.
3. **Should defect D4 (`FixRules` covering all five date members) ship in V1?** This spec assumes yes, because otherwise the M4 date picker cannot be offered for four of the five date fields and the operator table in the README is wrong for them. It is a small, backward-compatible change.

---

*UI design contract authored 2026-07-25. Platform facts verified against `jellyfin/jellyfin-web` and first-party `jellyfin/jellyfin-plugin-*` sources; items that could not be verified are labelled NEEDS VERIFICATION with a stated fallback.*
