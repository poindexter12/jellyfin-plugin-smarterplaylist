# UX Research: SmarterPlaylist In-Jellyfin Configuration Experience

**Date:** 2026-07-25
**Prepared for:** product-manager / UI design handoff
**Method:** Heuristic evaluation of the shipped experience + prior-art analysis + research plan. **No live user research (interviews, surveys, usability tests, or analytics) has been conducted for this plugin.** Every claim about current behavior below is cited to a specific file, function, or README section — nothing is inferred beyond what those sources state. Where a claim would require real users (mental-model fit, priority of missing properties, tolerance for preview latency), it is written as an assumption to validate, with a concrete cheap method, not as a finding. No participant counts, quotes, or metrics are fabricated anywhere in this document, because none were collected.

---

## 1. Target users and jobs-to-be-done

### The core segmentation error to avoid

Jellyfin self-hosters are technical enough to run a media server: install Docker or bare metal, manage libraries, sometimes configure reverse proxies and plugins. That is **operations skill**. It is not the same skill as hand-authoring a two-level disjunctive-normal-form (OR-of-AND) rule tree against a reflection-bound, case-sensitive property vocabulary documented only in a markdown table and a C# source file (`Jellyfin.Plugin.SmarterPlaylist/QueryEngine/Operand.cs`) — that is closer to **developer skill**. The current design implicitly assumes anyone who can run Jellyfin can also do the latter. Most Jellyfin admins are the former without being the latter. This is the single most important segmentation fact for the redesign: don't design for "our users are technical" as if that means "our users want to write JSON."

### Segments

**1. The Curator (primary, currently unserved).** Wants iTunes/Plex-style smart playlists — "unplayed CGP Grey videos," "movies from the last 2 years I haven't watched," "new music by artists I follow." JTBD: *"When my library grows faster than I can curate by hand, I want playlists that stay current on their own."* Comfortable with dropdowns, pickers, and a settings page. Not willing to hand-write JSON, memorize `PremiereDate`/`IsPlayed`/`MatchRegex`, or reason about OR-of-AND. Today's experience — a JSON file dropped into `<JellyfinDataPath>/SmarterPlaylists/` (README.md "Configuration") with zero UI (`Plugin.cs` no longer implements `IHasWebPages`, per `.planning/codebase/CONCERNS.md` "No plugin configuration page") — excludes this segment almost entirely. It's also the segment the feature is *named for* ("smart playlist" is a term this audience already has expectations about from iTunes/Plex), so it's the segment most damaged by the gap between name and experience.

**2. The Power Tinkerer.** Came from Emby/Plex/*arr-stack backgrounds, is fine with YAML/JSON-as-config if it's documented and validated (e.g., Radarr custom formats, Sonarr quality profiles). Will accept a file-based or advanced-JSON-editor path, but is *currently underserved too*: no validation exists (`.planning/codebase/CONCERNS.md` "No validation of definitions" — `SmarterPlaylist` has no validation step, a bad member/operator name throws only when the rule is compiled mid-run), so even this segment gets silent failures and log-diving rather than the fast, safe iteration loop tools like Radarr give them.

**3. The Automator/Scripter.** Smallest, most vocal segment — wants to generate or template definitions from an external script, keep them in git, or drive them from another tool. This is arguably why file-based JSON exists at all. Even this segment wants schema validation and non-destructive failure (one bad file currently aborts refresh for every playlist — see §2) rather than an opaque batch job.

**Explicitly not a current segment:** self-service by non-admin household members. Per-user partitioning is implied by the file system API (`GetSmarterPlaylistFilePaths(userId)`, `GetSmarterPlaylistPath(userId, playlistId)`) but the `userId` parameter is ignored entirely — all definitions live flat in one directory, keyed only by filename, with ownership enforced by nothing but a trusted `User` string field in the JSON (`.planning/codebase/CONCERNS.md` "Per-user partitioning is implied but not implemented"). Designing a multi-user self-service flow today would be building UI on a structural guarantee that doesn't exist yet. Treat as out of scope until that's fixed (see §6, N5).

### Representative JTBD statements (grounded in the feature's own stated purpose, README.md "Overview": *"an attempt to make a smarter playlist similar to what iTunes, Plex, and other media players have"*)

- "When I finish watching/listening to something, I want a playlist that already reflects what's next, without me rebuilding it."
- "When I add new rules or fix a broken one, I want to know immediately whether it worked — not 30 minutes later, and not by reading a server log."
- "When a playlist is empty or wrong, I want to know *why* a specific item was or wasn't included."
- "When I no longer want a playlist, I want removing it to be one action, not a manual two-step file-delete-plus-orphan-cleanup."

---

## 2. Usability failures in the current experience

Each item below is a specific mechanism, cited to source.

**No entry point at all.** `Plugin` does not implement `IHasWebPages`; there is no configuration page (`.planning/codebase/CONCERNS.md` "No plugin configuration page"; `.planning/codebase/ARCHITECTURE.md` "It does **not** implement `IHasWebPages` — there is no configuration page."). Creating a playlist requires filesystem access to `<JellyfinDataPath>/SmarterPlaylists/` — for Docker/NAS users this often means entering a container or mounting a volume. The feature is invisible inside the product it's a plugin for. This alone filters out the Curator segment before any rule-authoring problem is even reached.

**Silent-failure modes — several distinct mechanisms, not one:**
- *Compile-time-mid-run exceptions with no user-facing surface.* An invalid `MemberName` or `Operator` throws (`ArgumentException`/`MissingMethodException`) when the rule is compiled during the scheduled refresh, not when it's authored (`.planning/codebase/ARCHITECTURE.md`, "Secondary Path: rule compilation"; `.planning/codebase/CONCERNS.md` "No validation of definitions"). The only signal is a line in the Jellyfin server log.
- *One bad file kills every other playlist's refresh that run.* `ExecuteAsync` iterates definitions; anything thrown inside `RefreshPlaylistAsync` beyond the handled "unknown user" case propagates out and aborts the whole task (`.planning/codebase/ARCHITECTURE.md` "Error Handling — Gap"; `.planning/codebase/CONCERNS.md` "No validation of definitions — Blocks: … one bad file stops every other playlist from refreshing"). This is a blast-radius problem: one Curator's typo can silently stop refresh for every other user on a shared server.
- *Wrong-but-plausible results with zero error, historically.* Two now-fixed bugs illustrate the failure pattern the architecture is prone to: `MaxItems` was parsed, defaulted, and exposed but never applied — playlists silently weren't capped; `MatchRegex`/`NotMatchRegex` resolved `ToString()` on collection properties instead of testing each element, so regex rules against `Directors`/`Genres`/etc. silently never matched (and `NotMatchRegex` silently matched *everything*) (`.planning/codebase/CONCERNS.md` "Silent no-ops in the user-facing contract," "Known Bugs"). Both are fixed, but the pattern — the system produces a plausible wrong answer with no error at any layer — is architectural, not incidental, and any new UI inherits the risk unless validation is added at the boundary rather than trusted to "we fixed the known cases."

**Property-name discoverability.** `MemberName` must exactly match an `Operand` C# property name (PascalCase — `PremiereDate`, `IsPlayed`, `CommunityRating`, etc.), documented only in a README table and a link to the source file (README.md, "MemberName" bullet). There is no autocomplete, no enum, no case-insensitivity. A user builds one rule by cross-referencing two documents, gets no feedback on typos until the next scheduled run, and then only in the server log. This is a reflection-bound implementation contract (`.planning/codebase/ARCHITECTURE.md` "Reflection-bound contract. `Operand`'s property names and CLR types *are* the plugin's public API, resolved by string from JSON with no compile-time link") leaking directly into the end-user surface — the API is standing in for the UI.

**Operator/property legality is emergent, not declared, and only enforced at runtime.** Which operators are valid depends on the CLR type of the target `Operand` property (README.md, "Which operators work on which properties" table) — this is implementation detail, not a contract a user can consult ahead of time short of reading that table exactly. Worse, the README itself admits the surface is *wider* than what's sensible: "`Equal` and `NotEqual` come from the LINQ expression operators, so any name from that list is accepted, but only the ones above make sense for a given property type" — meaning invalid-but-accepted operator names won't error, they'll just misbehave. A user cannot tell "this is wrong" from "this is technically legal but silently useless" without already knowing the internals.

**OR-of-AND (DNF) is not most users' default mental model.** `ExpressionSets` (OR'd together) each containing `Expressions` (AND'd together) is genuinely expressive (arbitrary DNF), but it requires understanding boolean normal forms to use correctly. The README's own example proves the trap: to get "CGP Grey OR Nerdwriter1, always unplayed," the user must **duplicate** the `IsPlayed: False` expression into *both* `ExpressionSets` (README.md lines 28-57). Forgetting to copy a shared AND-condition into a new OR-branch is an easy, silent bug — nothing marks the two `IsPlayed` clauses as "the same condition, intentionally repeated," so a future edit to one and not the other produces divergent, hard-to-notice behavior.

**No preview or dry-run of any kind.** The only feedback loop is: edit the JSON file blind → wait up to 30 minutes, or manually trigger "Refresh all SmarterPlaylists" from *Dashboard → Scheduled Tasks* (README.md "Configuration") → go inspect the resulting Jellyfin playlist → repeat. There is no way to distinguish "0 items matched because of a typo" from "0 items matched because the criteria are legitimately too narrow" without this full round trip.

**No real delete flow.** `ISmarterPlaylistStore.Delete` exists but has no caller anywhere in the codebase (`.planning/codebase/CONCERNS.md` "Store API is largely dead code" — "notably `Delete`, which nothing ever invokes, so definitions are never cleaned up by the plugin"). Removing a playlist today is a manual, undocumented two-step: delete the JSON file yourself, and separately deal with the now-orphaned Jellyfin playlist, since nothing in the plugin ever triggers cleanup.

**Formatting churn on save.** The first refresh after creating a definition rewrites a hand-formatted file into a single minified line (`.planning/codebase/CONCERNS.md` "Hand-authored JSON is reflowed to minified JSON"). Minor, but real friction for the Tinkerer/Automator segments who keep these files in git or re-edit them by hand — diffs become unreadable.

---

## 3. Prior art: what to adopt, what to avoid

**iTunes / Apple Music Smart Playlists — primary model to borrow.** Convention: "Match **[all/any]** of the following rules," each rule is one row — property dropdown, operator dropdown *that changes options based on the chosen property's type*, and a value control appropriate to that type. Rows add/remove with +/−. A "Limit to N items, selected by [sort]" control. Nested rule groups (true OR-of-AND) exist but are a later, secondary/advanced feature — the default and majority use case is a **single flat list under one ALL/ANY toggle**.
- **Adopt:** default the builder to one flat rule list with one ALL/ANY toggle — this maps directly onto SmarterPlaylist's data model as *one `ExpressionSet`*. Filtering the operator dropdown to what's legal for the chosen property's type eliminates two of the audit's failure modes (property discoverability, operator/property legality) *by construction* — no documentation cross-referencing required, because the UI simply won't offer an illegal combination.
- **Adopt, as advanced-only:** "Add another rule group (matches if this OR any other group matches)" as an explicit, secondary action — this maps to adding a second `ExpressionSet`. Don't present OR-of-AND as the default shape; it's the power-user case, not the common case, even in the tool this plugin was explicitly inspired by.

**Plex smart collections/playlists.** Same rule-row builder, but value fields render as native, type-appropriate controls: date pickers for dates, star-rating widgets for ratings, and — critically — **multi-select/typeahead pickers sourced from actual values present in the library** for fields like genre, studio, director, actor, rather than free-text entry.
- **Adopt:** value pickers for list-typed properties (`Actors`, `Composers`, `Directors`, `Genres`, `GuestStars`, `Producers`, `Studios`, `Writers`) sourced from the user's real library data. This directly sidesteps a documented gotcha: "On a list property, `Contains` requires a whole exact element — `"Contains": "Grey"` will not match a director named `CGP Grey`" (README.md, operator notes). A picker that inserts the exact library value makes this failure mode structurally impossible; the user never needs to learn the rule exists.

**Emby smart playlists.** Rule-row builder with a **live match count** ("142 items match") that updates as rules change, before saving.
- **Adopt:** this is the cheapest, highest-leverage single feature identified in this document (see §4, flow 2, and §6 M5) — it converts "is my rule sane" from a 30-minute round trip into instant feedback, without needing a separate preview screen.

**Navidrome.** Useful as a *negative lesson about this plugin's own trajectory*, not a UI to copy: Navidrome's smart playlists originally shipped as hand-authored JSON/YAML files on disk before the project prioritized a UI builder — a recognized friction point in its own community. SmarterPlaylist is currently at the evolutionary stage Navidrome already grew out of; this validates that a UI is not a nice-to-have polish pass but closes a known, previously-solved gap.

**Spotify — negative prior art, don't imitate.** Spotify's "smart" surfaces (Discover Weekly, algorithmic radio) are opaque and non-editable; there is no user-authored rule tree at all. Its brand of "smart" trains users to expect zero-configuration curation, which is not what SmarterPlaylist offers or should try to offer. SmarterPlaylist's value proposition is explicit, auditable, editable rules — closer to iTunes/Plex than to Spotify. Don't chase "no rules needed" as a design goal here; that's a fundamentally different (much larger, ML-backed) project and importing that expectation would set the UI up to disappoint.

**General query-builder UX patterns (Airtable filters, Gmail search, JQL-style builders).**
- **Adopt:** validate per-field inline as the user builds the rule, not only on submit; disable/gray out operator options that are illegal for the selected property rather than allowing entry and failing later; keep the live "N items match" indicator persistent and non-blocking; render the current rule set as a plain-language sentence alongside the builder ("Show unplayed movies directed by CGP Grey OR Nerdwriter1") so the boolean structure is legible without mentally parsing nested boxes.
- **Avoid:** a free-text expression/DSL textbox (e.g., "write a WHERE clause") as the *primary* interface. That reintroduces exactly the discoverability failure this redesign exists to fix — it would just move the reflection-bound property vocabulary from a JSON file into a text box instead of removing it as a barrier. A DSL box is acceptable only as a Tinkerer-facing secondary/advanced mode with real-time validation, never as the default surface.
- **Avoid:** deep, arbitrary-depth nested AND/OR/NOT trees as the default. Every prior-art tool above gates this behind an explicit "advanced" step; SmarterPlaylist's engine can already express it (arbitrary DNF via `ExpressionSets`/`Expressions`), but expressive capability and default UI shape are different design decisions.
- **Avoid:** silent auto-correction of user input. If the system guesses what a user meant (e.g., coercing a bad date string), it must say so, not quietly substitute — this is the same "plausible wrong answer with no error" pattern flagged in §2 and should not be reintroduced by a well-meaning UI trying to be forgiving.

---

## 4. Key user flows a UI must support

**1. Create.** Entry point on the config page ("+ New Smart Playlist"). Name field; target-user selector as a dropdown of real Jellyfin users (not free text — eliminates the "unknown user, logged and skipped" failure mode described in `.planning/codebase/ARCHITECTURE.md` "Data Flow" step 4); one rule group by default with an ALL/ANY toggle; "+ Add rule" rows (property dropdown → operator dropdown filtered by the chosen property's type → value control matched to type, with pickers for list properties per §3); sort-order dropdown (`NoOrder`, `Release Date Ascending`, `Release Date Descending` today, per README.md "Order"); max-items field; Save.

**2. Preview/test before saving — the single highest-leverage flow.** A live (debounced) match count, ideally with a sample of matching titles, computed from the rule state currently in the editor, before anything is written to disk or scheduled. This is the direct antidote to the audit's core problems: a wrong property/operator combination, an empty result from a forgotten duplicate AND-condition, or a typo all become immediately visible as "0 matches" or an unexpected count, instead of a silent log entry discovered up to 30 minutes later.

**3. Edit.** Open an existing definition pre-populated into the same builder used for Create; re-run preview before re-saving. The published Jellyfin playlist still updates on the existing scheduled cadence, but the preview flow must be decoupled from that cadence so editing feels immediate.

**4. Debug why a specific item did or didn't match.** Given one media item and the current (saved or in-progress) rule set, show a per-rule-group, per-rule pass/fail breakdown — e.g. "Group 1: `IsPlayed Equal False` — FAIL (item is played); Group 2: `Directors Contains 'Nerdwriter1'` — PASS." This targets the operator/property-legality confusion and the OR-of-AND duplication trap head-on, and converts "why is my playlist empty" from a log-diving exercise into an answer the UI gives directly.

**5. Delete.** An explicit UI action that removes the definition file *and* makes an explicit, visible choice about the now-orphaned Jellyfin playlist (delete it too, or leave it as a static list) — this requires actually wiring the currently-dead `ISmarterPlaylistStore.Delete` member (`.planning/codebase/CONCERNS.md` "Store API is largely dead code") to a caller, and a product decision on orphan behavior, not just a UI affordance.

**6. List/validate (supporting, not a "flow" in the create/edit sense, but necessary and cheap).** A dashboard listing every definition with its name, target user, last-refresh outcome (success or the specific error), current item count, and next scheduled run time. This surfaces the currently-invisible mid-run exceptions per definition, converting "check the server log" into "look at a page," and is valuable even before a rule builder exists — see §6, M1.

---

## 5. Highest-risk assumptions to validate, and cheap ways to do it

1. **Assumption: Curators want a nested multi-group builder, not a flat list.** Risk: over-building UI complexity nobody uses, since a flat ALL/ANY list (iTunes-style) may cover the large majority of real playlists. *Cheap validation:* before building the group-management UI, audit real-world definitions already shared in the plugin's GitHub issues/discussions (or solicit a handful via a pinned issue) and count how many use more than one `ExpressionSet`. If nearly all real playlists are single-group, ship the flat list first and treat multi-group as a true advanced case rather than a mid-tier default.

2. **Assumption: a live "N items match" preview will feel instant.** Risk: the current architecture performs a full, unfiltered library scan per playlist per run (`.planning/codebase/CONCERNS.md` "Performance Bottlenecks — Full library scan per playlist, per run"); an interactive builder recomputing on every keystroke against a large library could feel slow — the opposite of the intended fix. *Cheap validation:* time `GetAllUserMedia` plus filtering against a representative large test library (e.g., 20k items) before committing to "live" preview; if too slow, debounce more aggressively, cache the per-user item set for the duration of the editing session instead of rescanning per keystroke, or preview against a capped/sampled subset with a clear "estimated" label.

3. **Assumption: users think in "match ALL/ANY," not "OR of AND."** This is copied from established prior art (§3), not yet tested against this specific audience, so treat it as medium-high risk rather than settled. *Cheap validation:* an informal five-user test (Jellyfin Discord/subreddit volunteers suffice) — describe the README's own CGP Grey example in plain English ("directed by CGP Grey or Nerdwriter1, not yet watched") and watch whether people can construct it correctly in a low-fidelity flat-list-with-groups prototype without being told the ALL/ANY toggle or "add group" affordance exists.

4. **Assumption: the current ~20 `Operand` properties cover what people actually want to filter on.** Risk: the README's own future-work section already flags known gaps — production year, official rating, tags, runtime, series name — as "cheap to extend" but not yet added. *Cheap validation:* tally property requests already present in GitHub issues/discussions to prioritize which `Operand` fields to add alongside the UI. This matters more once there's a UI: a dropdown makes a missing property visible and disappointing in a way a hidden JSON contract never was.

5. **Assumption: an admin-scoped config page (admin creates playlists for named users) is acceptable for V1, vs. true per-user self-service.** This is really a scope decision, not a research question — per-user partitioning isn't implemented at all (§1, §2), so a self-service flow today would sit on a missing structural guarantee. *Resolve with the maintainer directly*: is V1 "admin creates/edits definitions naming a target user" (matches today's data model, needs no backend change) or "each user manages their own" (requires the per-user partitioning fix first)? Settle this before flow design, not through user research.

---

## 6. Recommendations, prioritized

**Sequencing note up front:** the README's own future-work list already identifies the cheapest high-value step — *"a page that simply lists and edits the JSON would be a cheaper first step"* than a full rule builder. This document agrees and makes it explicit: **M1–M3 below can ship as a standalone first release**, before any rule-builder design or preview-performance work, and they close most of the silent-failure and discoverability gaps in §2 on their own.

### Must-have (V1)

- **M1 — Config page with a definitions list.** Table of every definition: name, target user, last-refresh outcome (success / specific error message), current item count, next scheduled run. Implements `IHasWebPages`. This single change converts every currently-invisible failure mode in §2 into something visible in the product, with the least design risk of anything on this list. Recommend building and shipping this *first*, independent of the rule builder, if timeline is a constraint.
- **M2 — Validate on save, not validate at refresh time.** Check `MemberName` against the real `Operand` property list and operator-per-type legality synchronously whenever a definition is saved — via the UI or by re-validating on load if hand-edited — and surface errors inline immediately, not only at the next scheduled run. This is the prerequisite for the whole surface to feel trustworthy; without it, the config page in M1 is still reporting yesterday's failure.
- **M3 — Fix the one-bad-file-aborts-everything blast radius.** A malformed or invalid definition must never block every other definition's refresh in the same run (already identified as a gap in `.planning/codebase/CONCERNS.md`). This is a correctness fix, not a UI feature, but it's load-bearing for the UI's credibility: a user who fixes their broken rule needs confidence every *other* playlist kept refreshing while theirs was broken.
- **M4 — Rule builder, flat-list-first.** Property dropdown → type-filtered operator dropdown → typed value control; single ALL/ANY toggle as the default surface; "add rule group" as an explicit advanced action for OR-of-AND. The filtered dropdowns eliminate the discoverability and operator-legality failure modes by construction (§2, §3).
- **M5 — Live-ish match preview.** Match count (ideally with a sample of titles) computed from the in-editor rule state before saving. Highest-leverage single flow identified in this document (§4, flow 2); validate performance per §5, assumption 2, before committing to fully live/keystroke-level updates.
- **M6 — Real delete flow.** Wires the currently-dead `ISmarterPlaylistStore.Delete` to an actual caller; UI makes an explicit choice about the orphaned Jellyfin playlist rather than leaving cleanup as an undocumented manual step.
- **M7 — Library-sourced value pickers for list properties.** Genre/studio/director/actor/etc. fields use autocomplete/multi-select sourced from real values in the user's library rather than free text, eliminating the "`Contains` requires a whole exact element" trap (§2, §3) structurally rather than by documentation.

### Nice-to-have (V1.5+)

- **N1 — Per-item "why did/didn't this match" debug view** (§4, flow 4). High value, but sequence after the builder exists since it's a diagnostic layered on top of it.
- **N2 — Plain-language sentence rendering** of the current rule set next to the builder (§3, query-builder patterns).
- **N3 — Deeper nested groups** beyond the two-level DNF the engine already supports. Only build if M1's usage data (once real definitions are visible) shows actual demand — don't build ahead of evidence per §5, assumption 1.
- **N4 — Additional `Operand` properties** (`ProductionYear`, `OfficialRating`, `Tags`, `RunTimeTicks`, `SeriesName`) and additional sort orders. Cheap on the engine side — a new `Operand` property is picked up automatically, a new `Order` is one file plus one switch arm (`.planning/codebase/ARCHITECTURE.md`, `Order` hierarchy) — but sequence *after* M4 so new fields land in a discoverable dropdown rather than another README row nobody reads.
- **N5 — Per-user self-service creation.** Blocked on the per-user partitioning structural fix (§1, §5 assumption 5); this is a scoping/architecture decision to resolve with the maintainer, not a UX design task, until that's fixed.
- **N6 — Pretty-printed JSON on save.** Fixes the minified-reflow friction (§2); low effort, benefits the Tinkerer/Automator segments who may keep hand-editing files even after a UI exists.

---

## Confidence summary

| Claim | Confidence | Basis |
|---|---|---|
| Current experience has no config UI, no validation, silent failures, and a blast-radius bug | High | Directly cited to code/README/architecture docs, not inferred |
| Segmentation (Curator / Tinkerer / Automator) and their JTBD | Medium | Reasoned from the plugin's stated purpose and Jellyfin's known self-hoster demographic; not validated with actual users — treat as a working hypothesis, not settled fact |
| iTunes-style flat-list-first is the right default | Medium | Strong, consistent prior art across iTunes/Plex/Emby; not yet tested against this specific audience — see §5, assumption 3 |
| Live preview is the single highest-leverage flow | Medium-High | Directly closes the most-cited failure mechanism (no feedback loop) and matches Emby's proven pattern; feasibility depends on unvalidated performance assumption — see §5, assumption 2 |
| Prioritized property/sort-order gaps (N4) | Low-Medium | Sourced from the README's own future-work list, not from tallied user requests — validate via §5, assumption 4, before investing |

## Open questions for the maintainer (not researchable without a decision)

- Is V1 admin-scoped (admin manages definitions naming a target user) or does it require true per-user self-service? Determines whether N5 blocks M1–M7 or can be deferred entirely.
- Should the plugin keep supporting hand-edited JSON files as a first-class parallel path once a UI exists (serving the Automator segment), or treat the UI as the sole source of truth going forward?
