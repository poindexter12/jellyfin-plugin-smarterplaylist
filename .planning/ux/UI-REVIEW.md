# UI-REVIEW: SmarterPlaylist Configuration Page

**Reviews:** `.planning/ux/UI-SPEC.md` (draft, 2026-07-25, 792 lines)
**Date:** 2026-07-25
**Reviewer stance:** adversarial. Starting hypothesis is that the spec contains unbuildable, unverified, or internally contradictory content until proven otherwise.
**Ground truth read:** `Operand.cs`, `Engine.cs`, `OperandFactory.cs`, `SmarterPlaylistDto.cs`, `SmarterPlaylist.cs`, `SmarterPlaylistStore.cs`, `SmarterPlaylistFileSystem.cs`, `ScheduleTasks/RefreshAllPlaylists.cs`, `Plugin.cs`, `Expression.cs`, `ExpressionSet.cs`, `OrderDto.cs`, `README.md`, `UX-RESEARCH.md`, plus a compiled probe against `Jellyfin.Controller 10.11.11` to settle nullability.

---

## Verdict

**Status: BLOCKED.** 8 blocking issues, 14 flags.

| # | Dimension | Verdict |
|---|---|---|
| A | Accessibility | **BLOCK** (2 blocking, 3 flags) |
| B | Verification honesty | **BLOCK** (3 blocking, 2 flags) |
| C | Implementability | **BLOCK** (3 blocking, 4 flags) |
| D | Forward compatibility | **FLAG** (1 flag) |
| E | Scope honesty | **FLAG** (3 flags) |
| F | Reported defects (D1–D7) | **FLAG** — all 7 are real; D2 is mis-evidenced; **two further defects are missing, one of them severe** |

This is a strong spec. It is better than most UI contracts at naming its own cost, at refusing to re-litigate settled constraints, and at recording rejected alternatives. The blocks below are not stylistic — each one is a place where an executor would either stop, guess, or ship something that does not work.

---

## A. Accessibility — BLOCK

### What is right, and verified independently

**A-P1 — The contrast arithmetic is correct. PASS.** Recomputed from scratch: `#c62828` relative luminance ≈ 0.13681; `#202020` ≈ 0.01444; ratio = (0.13681+0.05)/(0.01444+0.05) = **2.899:1**. The spec's "≈2.9:1" is exact, and its conclusion — below both 4.5:1 (1.4.3) and 3:1 (1.4.11) — is correct.

**A-P2 — The mitigation genuinely satisfies WCAG, not just nominally. PASS.** Three things have to hold together and all three do:
- **1.4.1 (Use of Color)** is satisfied because §6.3 gives every one of the seven statuses a *text label*, not just an icon and not just a colour. The status is recoverable with colour perception removed entirely.
- **1.4.11 (Non-text Contrast)** is not triggered by the sub-3:1 icon, because the icon is `aria-hidden="true"` and is redundant with adjacent text — a graphical object that is not "required to understand the content" is explicitly exempt. The spec reaches the right answer, and reaches it for the right reason.
- **1.4.3 (Contrast Minimum)** is satisfied because error *message text* is mandated to `--jf-palette-text-primary`, and the error colour is confined to a left border and icon fill. §6.4's `border-left: 3px solid` is decoration and carries no information the status cell does not.

The reasoning is sound. Do not weaken it.

**A-P3 — The focus indicator holds, though the spec never says so.** `#00a4dc` on `#202020` ≈ **5.7:1**, comfortably over the 3:1 that 1.4.11 requires of focus indicators. Worth recording in the spec so nobody "tones it down" later.

**A-P4 — Keyboard, labelling and error announcement are largely specified rather than asserted.** §15 gives concrete, testable commitments: `aria-expanded`/`aria-controls` on the disclosure button, unique accessible names per row via `clipForScreenReader` including the definition name, `aria-invalid` plus extended `aria-describedby` on errored fields, no positive `tabindex`, `role="alert"` emptied-and-repopulated for user-initiated diagnostics, `aria-live="polite"` for the summary and explicitly *not* for per-row diagnostics at load. The refusal to trap Tab in the textarea (§7.3, §15) is correct and correctly justified. The decision not to build a modal (§5) removes the focus-trap risk entirely rather than promising to get it right.

### BLOCK A1 — §7.4 and §15 give directly contradictory post-save focus instructions

- §7.4, Save, on `200`: *"keep the panel open, **move focus to the panel heading**."*
- §15, Focus management: *"After a successful save, **focus stays where it is**; the toast announces the result. **Never yank focus on success.**"*

These cannot both be implemented. An executor will pick one at random, and the two readings have opposite accessibility consequences: moving focus to a heading after a successful save is exactly the "yank" §15 forbids, and would drop a keyboard user out of the editor they are still working in.

**Fix:** delete the focus move from §7.4. §15 is right — after a successful save, leave focus in place and announce via a live region (see A2 below, which this depends on).

### BLOCK A2 — the secondary-text contrast claim is asserted, is stated in the wrong theme, and is load-bearing on mobile

§15 states: *"Secondary text uses `--jf-palette-text-secondary`, which the themes define as `rgba(0,0,0,0.87)` / theme default rather than a low-contrast grey."*

Two problems, and they compound:

1. `rgba(0,0,0,0.87)` is a **light-theme** value. Every other contrast statement in this document is argued against the **dark** theme's `#202020` paper background — including the load-bearing `#c62828` finding two paragraphs earlier. On the dark theme, `--jf-palette-text-secondary` is a *translucent white*, and its effective contrast is unstated and unmeasured. The spec measured the one token it was suspicious of and asserted the other, in the wrong theme. That is precisely the asymmetry this document's own §0 exists to prevent.
2. The token is not decorative. §6.2 and §6.4 put in `--jf-palette-text-secondary`: the `.json` filename line at `0.9em`, `(unnamed)`, `No rules`, `—`, and `Unknown since restart`. Worse, §12 at `<640px` folds **user, rules summary, item count and last refresh** into the Name cell "as stacked lines at `0.9em` in `--jf-palette-text-secondary`". On a phone or a tablet in portrait, essentially the entire informational payload of the page is rendered in an unmeasured translucent secondary colour at roughly 14px. That is body text and needs 4.5:1.

**Fix:** measure `--jf-palette-text-secondary` against `--jf-palette-background-paper` **in the dark theme** and record the number with the same rigour as the `#c62828` finding. If it is below 4.5:1, §12's `<640px` rule must promote the folded content to `--jf-palette-text-primary` — the visual hierarchy at that breakpoint has to come from size and order, not from a colour that may not be readable.

### FLAG A3 — save confirmation may be silent for screen-reader users

§15 relies on *"the toast announces the result"* for successful saves, and simultaneously forbids moving focus. But nothing in §3.5 or §15 establishes that `Dashboard.alert`'s toast form renders into a live region. It is asserted platform behaviour with no citation and no fallback — the only such case in the document, and it is the one where the consequence is a screen-reader user getting **no confirmation at all** that their save succeeded.

**Fix:** add a page-owned `<p role="status" class="clipForScreenReader">` that the page writes `Playlist definition saved.` into on success, independently of whatever the toast does. Cheap, fully under the page's control, and removes the dependency.

### FLAG A4 — the tab pattern is specified two-thirds of the way

§7.1 and §15 give arrow keys, Home/End, `role="tab"`, `aria-selected`, `aria-controls`, and `tabindex="0"` on the panel. Missing: **roving `tabindex`** (inactive tabs must be `tabindex="-1"`, active `tabindex="0"`), and whether activation is **automatic** (focus changes the panel) or **manual** (Enter/Space required). §15 says "Enter/Space activates", implying manual, while §7.1 says arrows "move between tabs" without saying whether the panel follows. Without roving tabindex, Tab lands on every tab button and the arrow-key contract is incoherent.

**Fix:** state roving tabindex explicitly and pick manual activation (correct here — the Advanced panel is expensive to render and should not be built on a stray arrow key).

### FLAG A5 — §12 and §15 disagree on the minimum viewport

§12: *"No horizontal scrolling of the page at any width down to **360px**."* §15: *"usable at 200% browser zoom and at a **320px** CSS viewport width without horizontal scrolling."* WCAG 1.4.10 (Reflow) requires 320px. §12 is the weaker number and is the one an implementer will test against.

**Fix:** make both 320px.

---

## B. Verification honesty — BLOCK

The three-way VERIFIED / NEEDS VERIFICATION / DERIVED labelling in §0 is a genuinely good instrument and mostly used with discipline. Every claim labelled DERIVED against this repository checked out correctly (see §B-P1). Every claim labelled NEEDS VERIFICATION carries a usable fallback (§B-P2). The failures are all in the **VERIFIED** column — claims that are unlabelled or over-labelled, which is the more dangerous direction.

**B-P1 — All DERIVED claims about this repo are correct. PASS.** Checked individually:
- OR/AND semantics and the two empty-set edge cases — correct, `SmarterPlaylist.cs:108` `compiledRules.Any(set => set.All(...))`.
- `Contains` on a list is whole-element, ordinal, case-sensitive — correct; `Engine.BuildExpr` resolves `Collection<string>.Contains(T)`.
- Regex is element-wise with `Any`, `NotMatchRegex` is `Not(Any)` — correct, `Engine.BuildRegexExpr:158-183`.
- Operator legality is emergent from CLR type, not declared — correct.
- `RefreshAllPlaylists.Key` returns `nameof(RefreshAllPlaylists)` — correct, line 77.
- `FindPlaylists` matches on the undashed id — correct, line 180.
- `Plugin.cs` does not implement `IHasWebPages` — correct.
- `FilterPlaylistItems` returns only the capped sequence, so the pre-cap count is unrecoverable — correct, line 114.
- `SmarterPlaylist`'s constructor silently falls back to `NoOrder` — correct, line 45.
- `SaveAsync` minifies — correct, `SmarterPlaylistStore.cs:57`, no `WriteIndented`.
- `Enum.TryParse(r.Operator, ...)` is case-sensitive and `GetProperty(name)` is case-sensitive — correct.

**B-P2 — Every NEEDS VERIFICATION item carries a real fallback. PASS.** `detailtable.scss` scope → scoped `<style>` fallback that works regardless. Material Icons subsetting → restricted to twelve core ligature names, with a text-only fallback and an explicit ban on substituting an SVG icon set. `ScheduledTasks` response shape → header degrades to a static sentence plus a link, list must not break. `pageshow` → `DOMContentLoaded`-guarded one-shot init. Globalisation tokens → V1 ships English with strings centralised. This is the standard the rest of the document should be held to.

### BLOCK B1 — `emby-radio` is used normatively and does not appear in the verified inventory

§3.3's own rule: *"Only these primitives may be used. Anything not listed here is either forbidden or must be added to this list after verification."* §13.3 then specifies the delete confirmation as *"`<fieldset>` + `<legend class="clipForScreenReader">` … **using `emby-radio`**."* `emby-radio` is absent from the §3.2 table. It is unlabelled, so by §0 it is "a design decision made by this spec, not a platform fact" — but it is written as a platform element name.

This is the exact failure mode flagged as highest-risk: a plausible-sounding Jellyfin custom element asserted without evidence. It may well exist. That is not the point; the spec's own gate says it must be verified and listed first.

**Fix:** verify `emby-radio` against `jellyfin-web/src/elements/` and add it to §3.2 with a citation, or respecify §13.3's radio group as plain `<input type="radio">` inside the fieldset. (M6-scoped, but the rule violation is V1's problem because it sets the precedent.)

### BLOCK B2 — §3.3 cites a token it declares does not exist

§3.3 states its token list is *"the complete emitted set"*. Six lines later, the load-bearing contrast finding cites `--jf-palette-background-paperChannel`, which is **not in that list**. Either the list is not complete — in which case "complete" is a false claim and the whole "only these primitives" gate is unsound — or the citation is invented, in which case the `#202020` background value underpinning the entire accessibility argument is unsourced.

The arithmetic downstream is correct (I verified it), so this is most likely an incomplete list rather than an invented token. But it cannot be left ambiguous in a document whose central discipline is that platform facts are cited.

**Fix:** either add the `*Channel` tokens to §3.3 and drop the word "complete", or re-cite the `#202020` value to `src/themes/dark/theme.scss` directly.

### BLOCK B3 — identical risk, opposite treatment: `emby-tabs` is rejected, `emby-textarea` is trusted with no fallback

§7.1 refuses `emby-tabs` on a stated risk: *"NEEDS VERIFICATION whether it initialises correctly outside the app's module graph."* That is good judgement.

But the **same risk applies verbatim** to `<textarea is="emby-textarea">` (§7.3) and `<div is="emby-collapse">` (§3.2) — customised built-in elements from the same `src/elements/` tree, all upgraded by the same module graph. `emby-textarea` is not merely used; it **is the entire V1 editing surface**. If it fails to upgrade, M2 has no save path and V1 ships without its only editor. The spec assigns it no fallback and does not label it as a risk at all, while rejecting a sibling element for that exact risk.

Separately: §3.2 is cited as *"Read from directory listing of `jellyfin/jellyfin-web` `src/elements/`"*. A directory listing establishes that a file exists. It cannot establish that `emby-input` *"Supports a `label` attribute"*, which §3.2 asserts as VERIFIED and which §15 then makes the primary labelling mechanism for every input on the page. The cited method does not support the claim made.

**Fix, two parts:** (1) `emby-textarea` degrades to a plain `<textarea>` with the page's own scoped styling if the upgrade does not occur — one line of contract, removes the single largest platform risk in V1. (2) Downgrade the `label`-attribute claim to NEEDS VERIFICATION with the fallback "explicit `<label for>`", which §15 already permits. This is a two-word change that makes the labelling contract safe.

### FLAG B4 — `Dashboard.confirm`'s signature is load-bearing and worth confirming directly

Three flows (Revert, unsaved-changes guard, Reload-from-disk) plus the §13.3 design rationale depend on `Dashboard.confirm(message, title, callback)` being **callback-style, not a promise** — the spec says so explicitly and marks it VERIFIED. `jellyfin-web` has migrated several of these helpers toward promise/module form over recent releases. If it is promise-returning on 10.11, three flows silently never fire their continuation.

**Fix:** confirm against the 10.11 tag specifically, and state a fallback (`Promise.resolve(Dashboard.confirm(...)).then(...)` handles both shapes).

### FLAG B5 — `Enum.GetNames<MediaType>()` does not name a type

§4.1 and §9 build the `TextEnum` control on `Enum.GetNames<MediaType>()`. `OperandFactory` imports `Jellyfin.Data.Enums` and assigns `baseItem.MediaType.ToString()`, whose type is `MediaBrowser.Model.Entities.MediaType` in 10.11. Two candidate types, one unqualified name, in the row that decides what a dropdown contains.

**Fix:** fully qualify it.

---

## C. Implementability — BLOCK

### What is right

The state coverage is unusually good. §6.4 specifies **loading, empty, populated, per-row error, page-level failure, and partial failure** — including the case most specs miss, where the list call succeeds but one row's live lookup fails server-side. The instruction not to call `Dashboard.processErrorResponse` on page-level failure (because it pops a modal over an empty page) is the kind of detail that only comes from thinking the state through. §11.8 ("every network call has a visible terminal state, no call may end in silence") is the right invariant and is actually honoured by the state table.

The control-by-property-type mapping in §9 is derived from the real CLR types rather than the README, and I confirmed it covers **all 20** `Operand` properties with correct types (`Collection<string>` ×8, `float` ×2, `double` ×5, `bool` ×1, `string` ×4). The operator lists are consistent with what `Engine.BuildExpr` will actually resolve — including the subtle case that `Equals` is not an `ExpressionType` member and therefore correctly falls through to `string.Equals(string)`.

### BLOCK C1 — S3 "New definition" is in V1 scope and is not specified

S3 appears in the §5 information architecture, has a dedicated endpoint (E3), has two CTA labels in §13.1, is the target of the empty state's primary button in §6.4, and is named in §1 as V1 in-scope ("a **New definition** flow seeded from a template").

There is **no section specifying it**. No field list. No statement of what the seed template contains. No states. No validation flow. No description of how `FileName` is chosen, whether `User` is a picker or free text, or what happens on `409`. §7 covers the detail panel only.

An executor cannot build S3 from this document, and E3's `400`/`409` responses have no consumer specified. Since S3 is the only creation path, V1 cannot ship without it.

**Fix:** add a §7.5 for S3 with the same rigour as §7: exact seed JSON template (it should be the README's CGP Grey example reduced to one group), field set, focus entry/exit, `409` handling, and cancel-with-unsaved-changes behaviour.

### BLOCK C2 — the `{fileName}` route key is ambiguous, and the codebase makes the ambiguity real

Six endpoints key on `{fileName}`. The spec never says whether that is the DTO's `FileName` field or the actual name of the file on disk. In this codebase they are two different things and can diverge:

- `SmarterPlaylistFileSystem.GetSmarterPlaylistPath(userId, playlistId)` (line 62-65) **ignores `userId`** and returns `Path.Combine(BasePath, $"{playlistId}.json")`.
- `SmarterPlaylistStore.SaveAsync` (line 52) calls it as `GetSmarterPlaylistPath(smarterPlaylist.Id, smarterPlaylist.FileName)` — the *Id* is passed as the userId and discarded, and the **`FileName` field** decides the path.
- Enumeration (`GetAllSmarterPlaylistFilePaths`) returns whatever `*.json` files exist, by their real names.

Consequences the spec does not address:
1. A user drops `foo.json` containing `"FileName": "bar"`. The first refresh writes `bar.json` and leaves `foo.json` in place — **two definitions where the user authored one**, both listed by E1, both refreshing.
2. If `FileName` is empty (the DTO default), `SaveAsync` writes `BasePath/.json`.
3. Editing `FileName` in the Advanced textarea and pressing Save is a **rename**. §8.2 E03/E04 validate the *format* and *collision* of `FileName` but say nothing about whether E4 renames the file, deletes the old one, or writes a second file. E04 is explicitly "create only", so the rename-collision case is unvalidated.

This is not a hypothetical. The Advanced JSON textarea is V1's only editing surface, so `FileName` is directly and freely editable on day one.

**Fix:** state that `{fileName}` is the **on-disk name without extension** and is the identity of the definition; that E4 rejects any request whose body `FileName` differs from the route segment (`400`, "Rename is not supported. Delete and recreate."), or, if rename is wanted, specify it as an explicit atomic operation. Add a validation rule that `FileName` must equal the on-disk name, surfaced as a blocking error on load, so pre-existing divergent files are visible rather than silently duplicated.

### BLOCK C3 — the E6 `Kind` derivation rule contradicts §9 for four of the five date members

§4.1's server-side derivation table:

| Condition on the `Operand` property | `Kind` |
|---|---|
| `typeof(double)` and **name ends with `Date` or is `PremiereDate`** | `Date` |
| numeric (`float`, `double`, `int`) | `Number` |

The four members in question are `DateCreated`, `DateLastRefreshed`, `DateLastSaved`, `DateModified`. None of them **ends** with `Date` — they end with `Created`, `Refreshed`, `Saved`, `Modified` — and none is `PremiereDate`. The rule as written classifies all four as **`Number`**.

§9 asserts all four are `Kind: Date` with a date picker. §4.1's `DateRewritten` note also assumes they are Date-kinded. The two normative sections disagree, and E6 is the single artefact §14 calls "the most important forward-compatibility constraint in this spec".

**Fix:** replace the name-suffix heuristic with an explicit member set — `PremiereDate`, `DateCreated`, `DateLastRefreshed`, `DateLastSaved`, `DateModified` — and see D-F1 below, which argues the whole heuristic needs a safety net.

### FLAG C4 — "Overwrite anyway" has no hash to overwrite with

§7.4's conflict flow offers `Overwrite anyway`, which *"re-PUTs with the current hash"*. The client's hash is stale by definition — that is why it got the `409`. The server's new hash is what is needed, and E4's `409` response body is unspecified (the table gives only the status code).

**Fix:** specify that E4's `409` body returns the current `SourceHash` and, ideally, the current file contents so `Reload from disk` needs no second round trip.

### FLAG C5 — `CriticRating` is not a 0–10 value

§9 specifies one control for both rating fields: `<input type="number" step="0.1" min="0" max="10">`. `CommunityRating` is 0–10; Jellyfin's `CriticRating` is a **0–100 percentage** (it is populated from `baseItem.CriticRating`, the Metacritic/RT-style score). A `max="10"` control makes every realistic critic-rating rule unenterable.

**Fix:** split the row — `CommunityRating` 0–10 step 0.1, `CriticRating` 0–100 step 1.

### FLAG C6 — the Items cell conflates the two numbers §4.3 forbids conflating

§4.3 is emphatic: *"Item counts are two different numbers and the UI must not conflate them"* — `PlaylistItemCount` is live, `MatchedCount`/`AppliedCount` are from the last run. §6.2's Items cell then renders live `PlaylistItemCount` **or**, conditionally, `{AppliedCount} of {MatchedCount}` from the last run, in the same cell with no visual distinction. After an edit-and-save but before the next scheduled run, the cell can show last-run numbers that contradict the live playlist.

**Fix:** always render the live count as the primary value, and put the truncation fact in a separate, explicitly last-run-labelled affordance — e.g. `100` with a secondary line `capped from 342 at the last refresh`.

### FLAG C7 — `Dashboard.showLoadingMsg()` plus `<p role="status">Loading…</p>`

§6.4 fires both. Two simultaneous loading announcements to a screen reader. Minor, but it is the kind of thing the rest of §15 is careful about.

---

## D. Forward compatibility — FLAG

**D-P1 — The central claim is correct and correctly prioritised. PASS.** *"If V1 ships a hand-maintained property list instead of reflection over `Operand`, M4 and N4 both break."* This is right. `Operand`'s reflection-bound contract is confirmed in the type's own XML docs ("Property names on this type are the vocabulary available to `Expression.MemberName`"), and `Engine.BuildExpr` resolves members by `GetProperty(name)` at runtime. A hand-written list would drift on the first N4 property. Stating it as a hard requirement rather than a preference is the right call.

**D-P2 — M5/M6/M7 residual work is honestly scoped. PASS.** E7–E9 are named as real, unbuilt cost rather than hand-waved. The M5 row correctly defers to UX-RESEARCH §5 assumption 2 rather than promising "live" preview, and pre-commits to an `estimated` label if measurement goes badly. The M6 row correctly notes `ISmarterPlaylistStore.Delete` would be getting its first ever caller — confirmed, nothing invokes it.

**D-P3 — The N1/N3/N5 obligations in §14 are the right ones. PASS.** Carrying `ExpressionSetIndex`/`ExpressionIndex` on every diagnostic from day one is exactly what N1 needs and costs nothing now. "No V1 code may assume `ExpressionSets.Count <= 2`" is the correct N3 constraint. Keeping `User` first-class and never assuming "the current admin" is the correct N5 constraint given the page is admin-scoped.

### FLAG D-F1 — E6 is only half reflection-derived, and the half that is not will break N4

The spec's forward-compat guarantee (§14) is *"a new property or order appears in the UI with **zero front-end change**"*. That holds for the `Name` and `ClrType` fields, which are genuine reflection. It does **not** hold for `Kind`, `Operators` and `ValueControl`, which come from the §4.1 derivation table — a hand-maintained heuristic keyed partly on **property names** ("name is `MediaType`", "name ends with `Date`").

Test it against the properties N4 actually names:

| N4 property | Likely CLR type | Matches a §4.1 row? |
|---|---|---|
| `ProductionYear` | `int` | yes → `Number` |
| `OfficialRating` | `string` | yes → `Text` |
| `Tags` | `Collection<string>` | yes → `TextList` |
| `SeriesName` | `string` | yes → `Text` |
| **`RunTimeTicks`** | **`long`** | **no** — the numeric row lists only `float`, `double`, `int` |

A `long` property falls through every row and gets **no `Kind`**. So does any future `decimal`, `int?`, `DateTime`, or enum-typed member. What the front end does with a member that has no `Kind` is unspecified — most likely it renders nothing, or throws, for a property the engine will happily filter on.

This is the same class of failure the spec exists to eliminate: a plausible wrong answer with no error, one layer up.

**Fix, three parts:** (1) add a terminal fallback row — any unmatched type maps to `Kind: Text` with `Operators: []` and a `Notes` string saying the property is not yet supported by the builder, so it degrades visibly instead of silently. (2) Add a server-side unit test asserting that **every** public property of `Operand` maps to a non-null `Kind` — this makes N4 fail loudly at build time rather than quietly at runtime. (3) Replace the `Date` name-suffix heuristic with an explicit member set (see C3).

With those three, the §14 claim becomes true. Without them it is overstated.

---

## E. Scope honesty — FLAG

**E-P1 — Non-goals are disciplined. PASS.** §16 is a good list and does not creep into N1–N6. §1's split between "V1, shippable on its own" and "forward-compatibility only" is honest, and §14 makes the forward-compat obligations concrete rather than aspirational. The document does not pretend to specify M4–M7.

**E-P2 — The backend cost is stated, not hidden. PASS.** Constraint 5 (*"There is no HTTP API today… Every interactive behaviour in this spec requires a new `ApiController`"*) is exactly the honesty asked for, and §4.1's per-feature endpoint table means nobody can claim surprise. The `IRefreshStatusStore` decision in §4.3 explicitly names and prices its accepted cost (status unknown after restart) rather than glossing it. This is above-average scope honesty.

### FLAG E-F1 — V1 is large for a milestone the research called the cheapest first step

UX-RESEARCH §6 opens by endorsing the README's own framing: *"a page that simply lists and edits the JSON would be a cheaper first step."* M1–M3 are explicitly offered as a standalone first release.

What this spec's V1 actually requires before anything ships:
- 6 new HTTP endpoints (E1–E6) on a plugin with **zero** current HTTP surface, plus a new `ApiController`, plus auth wiring.
- 7 backend defect fixes, all marked "**all must be fixed for V1**".
- A DI refactor (D3) introducing `IPluginServiceRegistrator` and three registered services.
- A **behavioural change to the rule engine** (D4).
- A **signature change to `FilterPlaylistItems`** (§4.3, to recover the pre-cap count).
- A new in-memory status store written from inside the scheduled task.
- Client-side: the list, 6 page states, a two-tab detail panel, a faithful OR/AND renderer, debounced server validation, an optimistic-concurrency conflict flow, manual task-run polling, and 3 responsive breakpoints.

Every item is individually justified. Collectively this is a substantial release, and the spec offers **no cut line** — no statement of what could ship first if the milestone has to be split. Given the research explicitly designed M1–M3 to be splittable, the spec should say how.

**Fix:** add a "minimum shippable subset" note. The natural cut is E1 + E2 + E6 + D1 + D3 + D5 + D6 (read-only list with server-side validation and per-definition error surfacing) as a first drop, with E3/E4/E5 and the editor following. That delivers M1 and most of M3's visible value without the concurrency flow, the editor, or D4.

### FLAG E-F2 — the free-text `User` field is an unnamed deviation from a research must-have

UX-RESEARCH §4 flow 1 is specific: *"target-user selector as a **dropdown of real Jellyfin users** (not free text — eliminates the 'unknown user, logged and skipped' failure mode)."*

In V1 the only editing surface is the Advanced JSON textarea, so `User` is free text. The spec's mitigation is real (E1/E2 detect unknown users server-side via `IUserManager.GetUserByName` and E06 blocks the save), and that is genuinely better than the status quo — the failure moves from "silently skipped 30 minutes later" to "refused at save time".

But §7.3's "Judgement call, stated because it is contestable" paragraph defends the textarea against UX-RESEARCH §3's anti-DSL guidance and **does not mention that the same decision also defers a named must-have flow element**. The document is otherwise scrupulous about naming its deviations; this one is missing.

**Fix:** name it in §7.3, and consider promoting `User` out of the JSON into a dedicated `<select is="emby-select">` above the tabs in V1. It is one field, `ApiClient.getUsers()` is already a dependency, and it closes the research's most-cited create-flow failure mode without waiting for M4.

### FLAG E-F3 — the proposed D4 fix silently regresses existing files

§4.4 D4 proposes: apply the date rewrite to all five members, *"but **only when `TargetValue` does not already parse as a number**, so existing files storing raw Unix seconds keep working."*

That guard changes the meaning of values that are legal today. `Engine.FixRules` currently calls `DateTime.Parse(rule.TargetValue, InvariantCulture)` **unconditionally** for `PremiereDate`. So `"PremiereDate": "2020"` parses today as **1 January 2020** → `1577836800`. Under the proposed guard, `"2020"` parses as a number and is left alone → the rule compares against **2020 seconds after the epoch**, i.e. January 1970. Every item matches `GreaterThan`. Silent, plausible, wrong — the exact pattern §8.2 E14 refuses to reintroduce elsewhere.

Bare years are a realistic thing for a user to have written, precisely because the README says `"2020-07-01"` works and does not say partial dates do not.

**Fix:** make the guard narrower and state it in the spec: rewrite unless the value is an integer **≥ some epoch-plausibility threshold** (e.g. ≥ 100000, which no real year-as-date is), and add unit tests for `"2020"`, `"2020-07-01"`, and `"1593561600"`. Also see F-D8 below, which is a related and more urgent problem in the same function.

---

## F. Reported defects, verified against source

All seven are real. One is mis-evidenced. Two significant defects are **missing**.

| ID | Verdict | Evidence |
|---|---|---|
| D1 | **REAL** | `RefreshAllPlaylists.ExecuteAsync` lines 106-110: `foreach (var dto in dtos) { … await RefreshPlaylistAsync(dto); }` with no `try`/`catch`. Anything thrown — and `Engine.CompileRule` throws `ArgumentException` / `MissingMethodException`, `Convert.ChangeType` throws `FormatException` / `InvalidCastException`, `new Regex` throws `ArgumentException` — aborts the whole task. Every subsequent definition is skipped. |
| D2 | **REAL, but the stated evidence is wrong** | See below. |
| D3 | **REAL** | `RefreshAllPlaylists` ctor line 64: `_plStore = new SmarterPlaylistStore(new SmarterPlaylistFileSystem(serverApplicationPaths));`. Nothing is injected; a controller would construct a second, independent instance. |
| D4 | **REAL** | `Engine.FixRules` at `HEAD` lines 76-83 tests `rule.MemberName == nameof(Operand.PremiereDate)` only. The other four are `double` Unix seconds with no rewrite, so `Convert.ChangeType("2020-07-01", typeof(double))` throws `FormatException` at compile time. The README's "Number" row does lump all five together and is therefore misleading. **Note: there is an uncommitted, in-progress fix in the working tree** (`Engine.cs` has a new `_dateMembers` array) that currently **fails the build** with `error CA1823: Unused field '_dateMembers'` — the field is declared but not yet consumed. |
| D5 | **REAL** | `SmarterPlaylistFileSystem.cs:38` — `Directory.GetFiles(...).First()` throws `InvalidOperationException` on no match. Would surface as `500`. |
| D6 | **REAL, and correctly characterised as newly reachable** | `GetSmarterPlaylistPath` (line 64) does `Path.Combine(BasePath, $"{playlistId}.json")` with no sanitisation, and `Path.Combine` with a rooted or `..`-bearing second segment escapes `BasePath`. The proposed `^[A-Za-z0-9._-]{1,64}$` plus explicit `.`/`..` rejection is the right control. **Extend it:** the same unsanitised value also reaches `GetSmarterPlaylistFilePath` (line 38) where it becomes a `Directory.GetFiles` **search pattern** with `SearchOption.AllDirectories` — a different but equally real failure — and `SmarterPlaylistStore.Delete` (line 64), which is M6's delete path. Sanitise at the boundary for all three. |
| D7 | **REAL** | `SmarterPlaylistStore.SaveAsync:57` — `JsonSerializer.SerializeAsync(writer, smarterPlaylist)` with default options, no `WriteIndented`. The Advanced editor would open on one line without E2's pretty-printing. |

### D2 — real risk, wrong evidence, overstated impact

The spec says: *"both are nullable, while `Operand.Album` / `Operand.FolderPath` are non-nullable `string`. Any rule using those members throws `NullReferenceException` on the first item with a null value."*

I compiled a probe against `Jellyfin.Controller 10.11.11` with `<Nullable>enable</Nullable>`:
- `string a = b.Album;` and `string f = b.ContainingFolderPath;` produce **no `CS8601`**.
- `b.Album = null;` produces **no `CS8625`**.

Both outcomes together mean these members are **nullable-oblivious** (compiled under `#nullable disable`), not annotated as nullable. That matters for two reasons: it explains why this project builds clean under `TreatWarningsAsErrors` (the spec's framing implies it should not), and it means the compiler will never warn about this — so the fix has to be deliberate, not analyser-driven.

Runtime null is still genuinely possible: `ContainingFolderPath` is derived from `Path.GetDirectoryName(Path)`, which returns null for a root path, and `Album` is unset for non-audio items.

The **impact claim is overbroad**, though. Tracing `Engine.BuildExpr` with a null property value:
- `Equal` / `NotEqual` / the comparison operators → `Expression.MakeBinary` on two strings. A null left operand compares `false`. **No throw.**
- `Contains` / `StartsWith` / `EndsWith` → `Expression.Call(left, method, arg)` — an **instance** call on a null receiver. **`NullReferenceException`.**
- `MatchRegex` / `NotMatchRegex` on a string member → `BuildRegexExpr` non-collection branch calls `left.ToString()`. **`NullReferenceException`.**

So it is the method-call and regex operators that throw, not "any rule". The fix (`?? string.Empty` at both assignment sites in `OperandFactory`) is correct and is the right fix regardless.

**Recommend:** keep D2, correct the wording to "nullable-oblivious, so the compiler will not flag it" and narrow the impact to method-call and regex operators. Warning W04 stays appropriate.

### MISSING — D8: `PremiereDate` rules are destroyed on disk and then break permanently. Severe.

This is not in the spec and it should be, because it invalidates §7.2's date rendering and contradicts the README.

The sequence:
1. `RefreshPlaylistAsync` line 120: `var smarterPlaylist = new SmarterPlaylist(dto);`
2. `SmarterPlaylist`'s constructor line 38: `ExpressionSets = Engine.FixRuleSets(dto.ExpressionSets);` — `FixRules` mutates `rule.TargetValue` **in place**, on the DTO's own objects (`ExpressionSet.Expressions` is a get-only `Collection<Expression>` shared by reference).
3. On first creation (line 130-134, `dto.Id is null`), the method calls `await _plStore.SaveAsync(dto)` — writing the **mutated** DTO back to disk.

Net effect: the user's `"TargetValue": "2020-07-01"` is silently rewritten on disk to `"1593561600"`.

Then it gets worse. On the **next** run, the file loads with `"1593561600"`, and `FixRules` calls `DateTime.Parse("1593561600", InvariantCulture)` unconditionally — which throws `FormatException`. Combined with D1, that **aborts the entire scheduled task** for every definition.

So: any definition using `PremiereDate` works exactly once, then permanently breaks itself and takes every other playlist down with it. This is very likely why the README's example uses `PremiereDate` only as a sort order and never as a rule.

**Why the spec must cover it:** §7.2 mandates rendering *"the stored value **and** its human form: `Premiere date is at least 2020-07-01 (1593561600)`"* — but after the first refresh the stored value **is** the epoch number and the human form is gone. E2's round-trip and the Advanced editor's contents are both affected. And D4's proposed fix interacts with it directly.

**Fix:** (a) make `FixRules` non-mutating — return a normalised copy for compilation and never touch the DTO that gets persisted; (b) make the parse defensive (`DateTime.TryParse` first, fall through to numeric); (c) add a regression test that a definition with a `PremiereDate` rule survives two consecutive refreshes with its file unchanged. Add this to §4.4 as a V1 blocker — it is more damaging than D4, which it subsumes.

### MISSING — D9: `GetSmarterPlaylistAsync(Guid)` can never find a file

`SmarterPlaylistStore.GetSmarterPlaylistAsync(Guid id)` (line 18-23) calls `GetSmarterPlaylistFilePath(id.ToString())`, which globs for `{guid}.json`. But `SaveAsync` writes files named `{FileName}.json`. No file is ever named after a GUID, so this lookup matches nothing and throws `InvalidOperationException` from D5's `.First()` every time.

Minor in itself (nothing currently calls it), but E2 is the natural place someone would reuse it. The spec should say explicitly that E2 must resolve by on-disk file name and must not use `GetSmarterPlaylistAsync`. Related to C2.

---

## GSD six-pillar summary

| Pillar | Verdict | Note |
|---|---|---|
| 1 Copywriting | **PASS** | §13 is a genuine copy deck with exact strings. §13.4's error principles (name the thing, state the next action, never "an error occurred", never blame the user) are exemplary and the examples are concrete. Empty state names `{BasePath}` and offers both paths. `Save`/`Validate`/`Revert` are bare verbs, but they are correct for a settings footer inside Jellyfin's own dashboard conventions and the destructive action is `Delete definition`, correctly specific. |
| 2 Visuals | **PASS** | Focal point, hierarchy and precedence are all declared. §6.3's seven-status precedence ladder is unambiguous and evaluated top-down. Icon-only actions all carry a `title` plus a row-unique `clipForScreenReader` label. |
| 3 Color | **PASS** | Accent use is genuinely reserved, not blanket: error colour is confined to a left border and icon fill, with the "never the sole carrier" rule stated as mandatory. Refusing to invent a success token is the right call and is reasoned, not asserted. The contrast issue is booked under A2, not here. |
| 4 Typography | **FLAG** | No type scale is declared (correctly — the page inherits Jellyfin's). But three weights are specified: normal, `500` (§6.2 Name), `600` (§7.2 `ANY`/`all of`, §7.2 diagnostic location lines). More materially, `jellyfin-web` does not necessarily ship 500/600 faces for its UI font, so those weights may synthesise or snap to 400/700 — which would defeat §7.2's requirement that `ANY` and `all of` be "the most salient text in the block". Verify which weights the bundled font actually provides and pick from that set. |
| 5 Spacing | **FLAG** | No spacing scale is declared. Values are scattered inline across §6.2 (`9rem`, `14rem`, `11rem`, `10rem`, `7rem`), §12 (`gap: 0.5rem`, `min-height: 2.75rem`), §7.3 (`24em`, `14em`). None violates a 4px grid, so nothing is blocking, but §3.6's stylesheet budget permits "spacing" without constraining it. Declare the scale in §3.6. |
| 6 Registry safety | **PASS** | Not applicable and correctly so. No third-party component registry, no CDN, no bundler, no external icon set — §16.1 forbids all of it explicitly, and §3.6's stylesheet budget enforces it. |

---

## What has to change before planning

**Blocking:**
1. **A1** — remove the post-save focus move from §7.4; §15 is authoritative.
2. **A2** — measure `--jf-palette-text-secondary` against the **dark** theme paper background; if under 4.5:1, promote §12's `<640px` folded content to `--jf-palette-text-primary`.
3. **B1** — verify and list `emby-radio`, or respecify §13.3 with plain radio inputs.
4. **B2** — reconcile "complete emitted set" with the `--jf-palette-background-paperChannel` citation.
5. **B3** — give `emby-textarea` a plain-`<textarea>` fallback; downgrade the `label`-attribute claim to NEEDS VERIFICATION with the `<label for>` fallback.
6. **C1** — specify S3 (New definition) to §7's depth, including the seed template.
7. **C2** — define `{fileName}` as the on-disk name, and specify rename behaviour and the `FileName`-mismatch validation.
8. **C3** — replace the `Date` name-suffix heuristic with an explicit member set.

**Also required before this is safe to build (defect track, not UI):**
9. **F-D8** — make `Engine.FixRules` non-mutating and its parse defensive. Any definition with a `PremiereDate` rule currently self-destructs after one refresh and aborts the whole task. Add to §4.4 as a V1 blocker.
10. **D-F1** — add a fallback `Kind`, and a test asserting every `Operand` property maps to one.
11. The working tree currently **does not build** (`CA1823`, unused `_dateMembers` in `Engine.cs`).

**Recommended (non-blocking):** A3, A4, A5, B4, B5, C4, C5, C6, C7, E-F1, E-F2, E-F3, and the two typography/spacing flags.
