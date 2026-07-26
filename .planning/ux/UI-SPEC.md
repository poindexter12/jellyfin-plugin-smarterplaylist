# UI-SPEC: SmarterPlaylist Configuration Page

**Status:** draft (revision 2)
**Date:** 2026-07-25 (revised same day after `UI-REVIEW.md`)
**Implements:** `.planning/ux/UX-RESEARCH.md` M1–M3 (V1, full detail) and M4–M7 (forward-compatibility detail only)
**Consumers:** planner, executor, ui-checker, ui-auditor
**Surface:** `Jellyfin.Plugin.SmarterPlaylist/Configuration/configPage.html`, embedded resource, registered via `IHasWebPages`

Revision 2 clears the eight blocking findings in `.planning/ux/UI-REVIEW.md` and dispositions all fourteen
flags. Section 19 maps every finding ID to Fixed / Declined / Superseded. Several platform claims in
revision 1 were wrong; they are corrected here with citations, and the review's own premises are corrected
where measurement contradicted them.

---

## 0. How to read this document

Every claim about the Jellyfin platform in this spec is one of three kinds, and is labelled:

- **VERIFIED** — read from a source file in `jellyfin/jellyfin-web` or a first-party `jellyfin/jellyfin-plugin-*` repository, or measured from a compiled probe. The citation is given.
- **NEEDS VERIFICATION** — plausible, commonly used, but not confirmed by reading source. Implementers must confirm before relying on it, and a stated fallback is given.
- **DERIVED** — a consequence of this repository's own code, cited to file and symbol.

Anything not labelled is a design decision made by this spec, not a platform fact.

### 0.1 Verification method for revision 2

Revision 1 cited two files that **do not exist** (`src/themes/_base/_theme.scss`, `src/themes/_base/_palette.scss`).
That is exactly the failure mode §0 exists to prevent, and it is why revision 2 states its method:

| Instrument | What it establishes |
|---|---|
| Sparse checkout of `jellyfin/jellyfin-web` branch `release-10.11.z` at commit `35c0793ece3adbd247eab290ae1effab851f3d37` (2026-06-06) | Every `jellyfin-web` citation below is a file:line in that tree. If a path is not in that tree, the claim is not made. |
| `@mui/material@6.4.12` (the exact version in `jellyfin-web`'s `package.json:90`) driven with Jellyfin's own theme options, calling `theme.generateStyleSheets()` | The **actual** emitted `--jf-*` custom properties and their per-colour-scheme values |
| WCAG 2.1 relative-luminance arithmetic over those emitted values, with alpha compositing onto each scheme's `background.paper` | The contrast table in §3.3 |
| Compiled probe against `Jellyfin.Controller` / `Jellyfin.Model` 10.11.11 via reflection | CLR types of `BaseItem` members (§4.1, §9) |
| `DateTime.TryParse` / `double.TryParse` probe on the .NET runtime | The date-normalisation behaviour analysed in §4.4 (see the correction to the review's FLAG E-F3) |

Residual gap, stated rather than hidden: the .NET parse probe ran on the **.NET 10** runtime because that is
the only runtime installed on this machine; the plugin targets `net9.0`. Invariant-culture `DateTime.TryParse`
behaviour for the tested inputs is not known to differ between 9 and 10, but the executor should re-run the
probe under `net9.0` before relying on the `"2020"` case in §8.2 E16.

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

### 1.1 Minimum shippable subset — the cut line

`UI-REVIEW.md` FLAG E-F1 is **accepted**: V1 as specified is a large release for a milestone UX-RESEARCH §6
framed as "the cheapest first step", and revision 1 offered no cut line. Rather than quietly re-scope, the
cut is stated explicitly here and the milestone boundary is left to the maintainer.

**Drop 1 — "See what is wrong" (read-only).**

| Included | Excluded |
|---|---|
| E1, E2, E6 | E3, E4, E5 |
| D1 (per-definition `try`/`catch`), D3 (DI), D5 (`404` not `500`), D6 (path sanitisation) | D4 (already landed — see §4.4), D7 (pretty-print for display) |
| S1 list with all seven columns and all six page states | S2's Save/Validate/Revert footer, the `SourceHash` conflict flow, S3 |
| S2 detail panel with the **Rules** tab and the diagnostics block | The Advanced tab |
| Header, task sentence, "Run refresh now" | — |

Drop 1 delivers M1 whole and most of M3's visible value: every definition on disk is listed, validated
server-side, and its per-refresh failure is rendered. It requires no write path, so it needs no concurrency
model, no unsaved-changes guard, and no create flow. It is roughly half the client work and none of the
riskiest parts.

**Drop 2 — "Fix what is wrong" (editing).** E3, E4, E5, D7, the Advanced tab (§7.3), the footer (§7.4),
S3 (§7.5), the `409` flow. This completes M2.

The rest of this document specifies both drops. Where a requirement belongs only to drop 2 it is marked
**(drop 2)**. If the maintainer ships them as one release, nothing changes.

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

7. **The page HTML is passed through `globalize.translateHtml` before it is injected, and that function eats every `${…}` in the file.**
   *VERIFIED* — `src/components/ServerContentPage.tsx:38-40` fetches the page from the server and calls
   `globalize.translateHtml(html)`; `src/lib/globalize/index.js:246-272` scans the **whole string** for `${`,
   looks the key up in the **core** `jellyfin-web` dictionary, and replaces it. `translateKeyFromModule`
   (`:218-236`) returns **the bare key** when the lookup misses.

   Two hard consequences, both of which will otherwise produce silent, baffling bugs:

   - **The embedded script must not contain a single JavaScript template literal.** `` `${count} items` ``
     is rewritten to `` `count items` `` before the browser ever parses it, and the page runs with the wrong
     string and no error. Use string concatenation, or a tiny `format('{0} items', n)` helper, throughout.
     This is a **mandatory, testable constraint**: the built `configPage.html` must contain zero `${`
     sequences other than deliberate globalisation keys.
   - `${Token}` globalisation works, but only for keys that exist in `jellyfin-web`'s own dictionary
     (`defaultModule()` is the core module for a server-supplied page). Plugin-supplied translation
     dictionaries are **not** reachable this way. See §16.11.

8. **The page is injected into the live React app, inside the MUI `ThemeProvider`.**
   *VERIFIED* — the dashboard route `configurationpage` renders `<ServerContentPage view='/web/configurationpage' />`
   (`src/apps/dashboard/routes/routes.tsx:15,49`), which sits inside `RootAppLayout`'s `<ThemeProvider theme={appTheme}>`
   (`src/RootAppRouter.tsx:55-64`). The fetched HTML is handed to `viewManager.loadView`
   (`src/components/viewContainer.js:30-78`, which special-cases `options.url.includes('configurationpage')`).

   Consequences: the `--jf-*` custom properties in §3.3 **are** in scope on this page, and `pageshow`
   **does** fire on the view element (`viewManager.dispatchPageEvents(true)`, `src/components/viewManager/viewManager.js:186`,
   firing at `:62`). Both were NEEDS VERIFICATION in revision 1 and are now VERIFIED.

---

## 3. Verified platform inventory

Only these primitives may be used. Anything not listed here is either forbidden or must be added to this list after verification.

### 3.1 Page shell and layout classes

Structural conventions read from `jellyfin/jellyfin-plugin-tvdb` `Jellyfin.Plugin.Tvdb/Configuration/config.html`
and `jellyfin/jellyfin-plugin-ldapauth` `LDAP-Auth/Config/configPage.html`. Revision 2 additionally checked
each class against every `.scss` file in `jellyfin-web@release-10.11.z`, because a class name appearing in a
reference page proves only that someone wrote it, not that it does anything.

| Class / attribute | Carries styling in 10.11? | Purpose |
|---|---|---|
| `data-role="page" class="page type-interior pluginConfigurationPage"` | `page` / `type-interior` **yes** (`src/styles/dashboard.scss:49`); `pluginConfigurationPage` **no rule** | Root element of a plugin config page. Keep all three — `pluginConfigurationPage` is a convention other tooling greps for. |
| `data-role="content"` | attribute, not a class | Content wrapper |
| `content-primary` | **yes** (`src/styles/dashboard.scss:154`, `src/styles/site.scss:115`) | Primary column |
| `verticalSection` | **no rule** | Section container — structural only. Do not rely on it for spacing; use §3.6's scale. |
| `verticalSection-extrabottompadding` | **yes** (`src/styles/librarybrowser.scss:1277`) | Bottom padding on the last section |
| `sectionTitleContainer flex align-items-center` + `h2.sectionTitle` | **yes** (`librarybrowser.scss:1292,1296`; `src/styles/flexstyles.scss:1,25`) | Section heading row |
| `inputContainer` | **yes** (`src/elements/emby-input/emby-input.scss:32`) — but see §3.2 on lazy loading | Wrapper for a labelled input |
| `fieldDescription` | **yes** (`src/styles/site.scss:105`) | Helper text under a control |
| `checkboxContainer`, `checkboxContainer-withDescription`, `checkboxFieldDescription` | **yes** (`emby-checkbox.scss:16,20,29`) — lazily loaded | Checkbox row |
| `selectContainer` | **yes** (`emby-select.scss:72`) — lazily loaded | Wrapper for a select |
| `raised button-submit block` | **yes** (`emby-button.scss`, plus per-theme overrides e.g. `src/themes/dark/theme.scss` `.button-submit`) — eagerly loaded, see §3.2 | Primary submit button styling |
| `raised button-alt` | **yes**, same source | Secondary button styling |
| `headerHelpButton` | **no rule anywhere in 10.11** | Legacy hook. Harmless, but it styles nothing — the Help link's appearance comes entirely from `raised button-alt`. |
| `checkboxList paperList checkboxList-paperList` | **yes** (`emby-checkbox.scss:92,113`, `listview.scss:286`) | Scrollable list-of-checkboxes container. **Not used by this page** — kept in the inventory only so nobody re-derives it. |
| `textarea-mono` | **yes** (`src/styles/fonts.scss:25`) | Monospace stack. Loaded on every non-TV client (`src/index.jsx:127-138` — `fonts.scss` in two of three branches). **Not** present in the TV branch (`fonts.sized.scss`), so §3.6 must also declare its own stack. |

### 3.2 Custom elements — VERIFIED, with a registration caveat that changes how they may be used

Read from the element sources in `jellyfin/jellyfin-web@release-10.11.z` `src/elements/`, not from a
directory listing. Revision 1 cited "directory listing", which cannot establish an element's API; that
citation is withdrawn and every row below is read from the element's own `.js`.

**The caveat, and it is load-bearing.** Every one of these is a **v0 customised built-in** registered with
`document.registerElement(name, { extends: … })` behind the `webcomponents.js/webcomponents-lite` polyfill.
Registration happens when the element's module is imported — and `src/index.jsx` imports **only**
`./elements/emby-button/emby-button` (line 31). Every other element module is imported lazily by whichever
feature component needs it (`emby-input` 25 importers, `emby-select` 31, `emby-textarea` 3, `emby-radio` 2,
`emby-collapse` 4 — none of them this page, and none of them guaranteed to have loaded).

So on a cold load of the plugin configuration page, **any `is="emby-*"` element other than `emby-button` may
never upgrade**. This is not speculation; it is what the import graph says. The rule that follows:

> **Every `is="emby-*"` attribute on this page is progressive enhancement. The page must be fully usable,
> labelled and styled with the element unupgraded.**

| Element | Registered eagerly? | Behaviour if it does upgrade | Contract for this page |
|---|---|---|---|
| `<button is="emby-button">` / `<a is="emby-button">` | **Yes** — `src/index.jsx:31` | Adds `emby-button` class, ripple, router-aware link handling | Safe to rely on. Still write `class="raised button-submit"` etc. explicitly. |
| `<button is="paper-icon-button-light">` | **No** — `src/elements/emby-button/paper-icon-button-light.js` is imported by 54 feature modules, not by `index.jsx` | Adds the class `paper-icon-button-light` and `show-focus`, and nothing else (`paper-icon-button-light.js:8-11`) | **Write `class="paper-icon-button-light"` in the markup yourself.** The CSS lives in `emby-button.scss`, which *is* eagerly loaded via `emby-button.js:10`, so the styling lands even when the element does not upgrade. |
| `<input is="emby-input">` | No | Inserts a sibling `<label class="inputLabel">` whose text is the `label` **attribute**, sets `label.htmlFor = this.id`, and auto-assigns an `id` if absent (`emby-input.js:45-52,114`) | Use `is="emby-input"` for the look, but **label every input with an explicit `<label for>` in the markup**. Never depend on the `label` attribute — if the element does not upgrade, a `label` attribute is inert and the field has **no accessible name at all**. |
| `<select is="emby-select">` | No | Same label mechanism (`emby-select.js:118-122,135`) | Same contract: explicit `<label for>`. |
| `<input type="checkbox" is="emby-checkbox">` | No | MDL checkbox chrome | Wrap in `<label>` with a `<span>` for the text; degrades to a native checkbox. Not used in V1. |
| `<input type="radio" is="emby-radio">` | No | MDL radio chrome. **Requires** its parent to be a `<label>` that contains a `<span>` — `attachedCallback` does `labelElement.querySelector('span')` then `.classList.add(...)` with no null check (`emby-radio.js:28-45`), so a missing `<span>` throws a `TypeError` during upgrade | **VERIFIED to exist** (`src/elements/emby-radio/emby-radio.js`, `document.registerElement('emby-radio', { extends: 'input' })` at `:71-74`). Usable, but only in the exact markup shape §13.3 now specifies. Degrades to a native radio. |
| `<div is="emby-collapse">` | No | Builds a collapse button and animates height | **Forbidden on this page.** It has no graceful degradation — unupgraded it is an inert `<div>` whose content is permanently visible with a stray `title`, and the disclosure affordance simply is not there. §7 hand-rolls disclosure with `aria-expanded`/`aria-controls` instead. Removed from the permitted list. |
| `<textarea is="emby-textarea">` | No | Sets `rows = 1`, adds class `emby-textarea`, inserts a label, and attaches an unconditional **`AutoGrow`** that recomputes height from content (`emby-textarea.js:99-128`) | **Forbidden for the Advanced editor.** Not because it might fail to upgrade, but because if it *does* upgrade, `rows = 1` + `AutoGrow` actively fight the fixed-height, scrollable, resizable JSON editor §7.3 requires. §7.3 uses a plain `<textarea>` styled by the page. This is a stronger resolution than the fallback contract `UI-REVIEW.md` BLOCK B3 asked for, and it removes the risk in both directions. |

`emby-tabs` remains unused, for the reason revision 1 gave — and the reason is now stronger, not weaker:
it is in the same lazily-imported set, so a hand-rolled `role="tablist"` is the only shape that works on a
cold load.

### 3.3 Theme tokens — VERIFIED, and revision 1's account of them was wrong

**Correction.** Revision 1 said these tokens are "read from `src/themes/_base/_theme.scss` and
`src/themes/_base/_palette.scss`" and that its twenty-token list was "the complete emitted set". Both
statements are false. There is no `src/themes/_base/` directory in `jellyfin-web@release-10.11.z`. The
`--jf-*` custom properties are not authored in SCSS at all.

**What actually emits them.** `src/themes/themes.ts:125-134` calls MUI's `createTheme` with
`cssVariables: { cssVarPrefix: 'jf', colorSchemeSelector: '[data-theme="%s"]', disableCssColorScheme: true }`
and `defaultColorScheme: 'dark'`, over the six colour schemes in `COLOR_SCHEMES` (`themes.ts:115-122`).
The Jellyfin-specific values come from `src/themes/defaults.ts` (`DEFAULT_COLOR_SCHEME`), merged over MUI's
own default dark or light palette. So the emitted set is **whatever MUI 6.4.12 emits**, which is
**307 `--jf-palette-*` declarations for the dark scheme alone** — an order of magnitude more than revision 1
claimed, and it includes every `*Channel` variant.

`UI-REVIEW.md` BLOCK B2 asked to reconcile "complete emitted set" with the `--jf-palette-background-paperChannel`
citation. The reconciliation is: **the word "complete" was false and is withdrawn**, and
`--jf-palette-background-paperChannel` **does exist** — it is emitted as `32 32 32` on the dark scheme.
But it is also the wrong thing to cite for the value `#202020`, so the citation is replaced with the
primary source.

**Values this spec depends on** — all read from `src/themes/defaults.ts` and reproduced by running
`@mui/material@6.4.12` (the version pinned at `jellyfin-web` `package.json:90`) with Jellyfin's options and
calling `theme.generateStyleSheets()`:

| Token | Dark (default scheme) | Source |
|---|---|---|
| `--jf-palette-background-default` | `#101010` | `defaults.ts` `DEFAULT_COLOR_SCHEME.palette.background.default` |
| `--jf-palette-background-paper` | `#202020` | `defaults.ts` `DEFAULT_COLOR_SCHEME.palette.background.paper` |
| `--jf-palette-text-primary` | `#fff` | MUI dark default |
| `--jf-palette-text-secondary` | `rgba(255, 255, 255, 0.7)` | MUI dark default |
| `--jf-palette-error-main` | `#c62828` | `defaults.ts` `DEFAULT_COLOR_SCHEME.palette.error.main` |
| `--jf-palette-primary-main` | `#00a4dc` | `defaults.ts` `DEFAULT_COLOR_SCHEME.palette.primary.main` |
| `--jf-palette-warning-main` | `#ffa726` | MUI dark default |
| `--jf-palette-success-main` | `#66bb6a` | MUI dark default |
| `--jf-palette-divider` | `rgba(255, 255, 255, 0.12)` | MUI dark default |

**Second correction: success and warning tokens *do* exist.** Revision 1 asserted "There is no success token
and no warning token", and §6.3 leaned on it. That is false — MUI supplies `--jf-palette-success-*` and
`--jf-palette-warning-*` in every scheme. §6.3's *decision* survives (see §6.3), but it now rests on design
reasoning and on the contrast table below, not on a platform claim that was never true.

Always write tokens with a fallback: `color: var(--jf-palette-text-secondary, rgba(255,255,255,0.7));`

#### Contrast, measured across all six colour schemes

The single most serious methodological error in revision 1 was arguing every contrast claim against the
dark theme's paper background while quoting a **light-theme** value for `text-secondary`. Jellyfin ships
**six** colour schemes, all reachable from the dashboard theme picker, and the page must be legible in all
six. Each cell is the WCAG 2.1 contrast ratio of the foreground — alpha-composited where the token is
translucent — against that scheme's `background.paper`.

| Foreground token | dark (default) | light | appletv | blueradiance | purplehaze | wmc | Worst |
|---|---|---|---|---|---|---|---|
| `--jf-palette-text-primary` | 16.29:1 | 13.58:1 | 16.07:1 | 18.30:1 | 20.23:1 | 15.18:1 | **13.58:1** |
| `--jf-palette-text-secondary` | 8.59:1 | 5.39:1 | 5.74:1 | 9.22:1 | 9.76:1 | 8.06:1 | **5.39:1** |
| `--jf-palette-error-main` | 2.90:1 | 4.59:1 | 5.62:1 | 3.26:1 | 3.60:1 | 2.70:1 | **2.70:1** |
| `--jf-palette-warning-main` | 8.39:1 | 2.54:1 | 3.11:1 | 9.42:1 | 10.41:1 | 7.81:1 | **2.54:1** |
| `--jf-palette-success-main` | 6.89:1 | 4.18:1 | 5.13:1 | 7.74:1 | 8.55:1 | 6.42:1 | **4.18:1** |
| `--jf-palette-primary-main` | 5.70:1 | 2.33:1 | 2.86:1 | 6.40:1 | 7.07:1 | 5.31:1 | **2.33:1** |

Paper backgrounds used: dark `#202020`, light `#e8e8e8`, appletv `#ffffff`, blueradiance `#011432`,
purplehaze `#000420`, wmc `#0c2450` (`src/themes/themes.ts:42-112`). `text-primary`/`text-secondary` take
MUI's dark values (`#fff`, `rgba(255,255,255,0.7)`) on the four dark-mode schemes and MUI's light values
(`rgba(0,0,0,0.87)`, `rgba(0,0,0,0.6)`) on `light` and `appletv`.

Four mandatory consequences:

1. **`--jf-palette-error-main` is unusable as an information carrier.** Worst case **2.70:1** (wmc), and it
   fails 3:1 on the default dark theme too. Revision 1's rule stands and is reinforced: error colour may
   appear only as a decorative left border or an icon fill that is redundant with adjacent text. Error
   **message text** is always `--jf-palette-text-primary`. Every error state is icon + text label.
2. **`--jf-palette-text-secondary` is safe as body text — worst case 5.39:1, above 4.5:1 in every scheme.**
   This settles `UI-REVIEW.md` BLOCK A2. Revision 1's claim was stated in the wrong theme and quoted a value
   (`rgba(0,0,0,0.87)`) that is in fact light-mode **text-*primary***, so the claim was worthless even though
   the conclusion happens to hold. §12's `<640px` fold may therefore keep secondary text; no promotion to
   `text-primary` is required. The instruction "do not add opacity on top of it" stays — the token is
   already 70% alpha and stacking would drop it below 4.5:1.
3. **No accent colour is safe across all six schemes.** `warning-main` is 2.54:1 on light, `success-main`
   4.18:1, `primary-main` 2.33:1. There is no palette colour that can carry meaning by itself. This is the
   general form of consequence 1 and is why §6.3's status system is icon + text everywhere.
4. **`--jf-palette-primary-main` must not be used for the focus indicator.** At 2.33:1 on the light scheme
   and 2.86:1 on appletv it fails WCAG 1.4.11's 3:1 for focus indicators. `UI-REVIEW.md` A-P3 said the focus
   indicator "holds" — that measurement was taken on dark only, and it does not generalise. §15 is corrected
   to use `--jf-palette-text-primary` (worst case 13.58:1) for any custom focus outline.

### 3.4 Utility classes — VERIFIED

| Class | Source | Purpose |
|---|---|---|
| `.material-icons` | `src/styles/site.scss:64` | Material Icons ligature span |
| `.clipForScreenReader` | `src/styles/site.scss:53` | Visually hidden, screen-reader-available text |
| `.detailTable`, `.detailTableHeaderCell`, `.detailTableBodyCell` | `src/styles/detailtable.scss:1,2,7` | Dashboard table styling |
| `.textarea-mono` | `src/styles/fonts.scss:25` | Monospace family stack (see §3.1 for its TV-branch gap) |

**Resolved — `detailtable.scss` is globally in scope.** It is imported unconditionally at app entry:
`src/index.jsx:46` `import './styles/detailtable.scss';`, alongside `site.scss`, `dashboard.scss` and
`librarybrowser.scss`. This was NEEDS VERIFICATION in revision 1 and is now VERIFIED. The scoped fallback in
§3.6 is **retained anyway** — it costs four declarations, and it is the only thing standing between this
page and a future release that route-splits the dashboard stylesheets.

**Resolved — Material Icons are not subsetted.** `jellyfin-web` depends on `material-design-icons-iconfont`
6.7.0 (`package.json:118`) and imports the package wholesale (`import 'material-design-icons-iconfont';`) from
more than a dozen always-reachable modules including `src/components/actionSheet/actionSheet.ts:8`. The full
Material Design ligature set is available. The twelve names this page uses — `check_circle`, `error`,
`warning`, `schedule`, `edit`, `delete`, `add`, `refresh`, `expand_more`, `expand_less`, `visibility`,
`open_in_new` — are all in that set.

**Residual risk, downgraded but not zero:** the font is loaded by *modules*, not by `index.jsx`, so on a very
cold load the face may not yet be applied when the page first paints. Every icon in this spec is
`aria-hidden="true"` and redundant with an adjacent text label (§6.3), so a briefly-unstyled ligature is a
cosmetic flash, not a loss of information. The standing ban holds: if a ligature renders as literal text at
implementation time, fall back to a text-only status label — never to an image or an inline SVG icon set.

### 3.5 JavaScript globals — VERIFIED

Read from `jellyfin/jellyfin-web` `src/utils/dashboard.js` (which assigns `window.Dashboard`) and confirmed in use by both reference plugin pages.

| Global | Members this spec uses |
|---|---|
| `window.ApiClient` | `getPluginConfiguration(id)`, `updatePluginConfiguration(id, cfg)`, `getUsers()`, `getUrl(path, queryObj)`, `getJSON(url)`, `ajax({type, url, data, contentType})` |
| `window.Dashboard` | `showLoadingMsg()`, `hideLoadingMsg()`, `alert(stringOrOptions)`, `confirm(message, title, callback)`, `processPluginConfigurationUpdateResult()`, `processErrorResponse(response)`, `navigate(url)` |

Notes, all VERIFIED by reading `src/utils/dashboard.js` at `release-10.11.z`:
- `Dashboard.alert('text')` shows a **toast**; `Dashboard.alert({title, message, callback})` shows a **modal alert** (`:170-180`). The two forms are not interchangeable — this spec states which is meant at every call site.
- **`Dashboard.confirm(message, title, callback)` is callback-style and returns `undefined`.** `:208-214` —
  `baseConfirm(message, title).then(() => callback(true)).catch(() => callback(false));`. It is not a promise.
  This settles `UI-REVIEW.md` FLAG B4 against the 10.11 tag specifically. No `Promise.resolve(...)` wrapper is
  needed; adding one would be harmless but is not required, and this spec does not ask for it.
- `Dashboard.processPluginConfigurationUpdateResult()` hides loading and toasts the localised "Settings saved" string. It is only correct after a *plugin configuration* save. This plugin's definitions are **not** plugin configuration, so this spec does not use it; see Section 7.4.
- `window.Dashboard = Dashboard` is assigned at `:262` with the comment *"This is used in plugins and templates, so keep it defined for now."* The global is intentional and supported, but the comment is a standing notice that it is on borrowed time.

**The toast is not announced to screen readers — VERIFIED, and it changes §7.4.** `src/components/toast/toast.ts`
creates a bare `<div class="toast">` inside a bare `<div class="toastContainer">` appended to `document.body`.
There is no `role`, no `aria-live`, no `aria-atomic`. `UI-REVIEW.md` FLAG A3 hedged that the toast "may be
silent"; measurement removes the hedge. It **is** silent. §7.4 and §15 therefore require a page-owned
`role="status"` region for every success announcement, and the toast is treated as visual-only.

**`Dashboard.showLoadingMsg()` announces nothing either — VERIFIED.** `src/components/loading/loading.ts`
`show()` toggles a class on a purely decorative MDL spinner `<div>`; again no `role`, no live region.
This is why §6.4 pairs it with `<p role="status">Loading…</p>` — that is the *only* announcement, not a second
one. See §19's disposition of FLAG C7.

**`Dashboard.dialogHelper` *is* exposed — correcting §5's stated reason.** `dialogHelper` is a member of the
`Dashboard` object (`:258`). Revision 1 rejected a modal on the grounds that `dialogHelper` "is not verifiably
reachable from a plain embedded config page". That reason was wrong. §5 keeps the inline-panel decision and
restates its real justification.

Page lifecycle: bind on the `pageshow` event of the root page element. **VERIFIED** — `viewManager` calls
`viewManager.dispatchPageEvents(true)` at module load (`src/components/viewManager/viewManager.js:186`), which
is what gates the `pageshow` dispatch at `:62`. Revision 1 marked this NEEDS VERIFICATION; it is confirmed.
Keep the `DOMContentLoaded`-guarded one-shot init as belt-and-braces — the page is injected into an already-loaded
document, so `DOMContentLoaded` will normally have fired and the guard costs three lines.

### 3.6 Scoped stylesheet budget, spacing scale and type scale

One `<style>` block inside the page root, every selector prefixed with `#SmarterPlaylistPage`. It may only:
- set layout (grid/flex, spacing, `overflow`, `min-height`) **from the scales below**,
- reference `--jf-palette-*` tokens with fallbacks,
- provide the `.detailTable` fallback described in §3.4,
- declare the monospace family for the Advanced editor (§7.3), because `.textarea-mono` is absent on the TV branch,
- define `@media` breakpoints from Section 12.

It may **not** define a colour literal other than as a `var()` fallback, set the *body* font family, or
restyle any `emby-*` element.

**Spacing scale — declared, closing `UI-REVIEW.md`'s pillar-5 flag.** Revision 1 scattered values inline and
declared no scale. All spacing on this page comes from this ladder and nothing else:

| Step | Value | Used for |
|---|---|---|
| `--sp-1` | `0.25rem` (4px) | Icon-to-text gap inside a cell |
| `--sp-2` | `0.5rem` (8px) | Gap between footer buttons; gap between stacked lines in a folded cell |
| `--sp-3` | `1rem` (16px) | Table cell padding; diagnostics list item spacing |
| `--sp-4` | `1.5rem` (24px) | Panel internal padding; gap between the tab strip and the tab body |
| `--sp-5` | `2rem` (32px) | Gap between page sections |
| `--sp-6` | `3rem` (48px) | Empty-state block padding |

Column widths in §6.2 (`7rem`–`14rem`) are *content sizing*, not spacing, and are exempt. The `2.75rem`
(44px) minimum touch target in §12 is an accessibility floor, also exempt.

**Type scale — declared, closing `UI-REVIEW.md`'s pillar-4 flag, and the weights are corrected.** The page
inherits Jellyfin's sizes and declares no new ones; the only sizes it sets are `0.9em` for secondary lines
and `0.95em` for the monospace editor.

Weights are a different matter, and revision 1 got them wrong. It specified three weights — normal, `500`
and `600`. **`jellyfin-web` bundles Noto Sans at 400 and 700 only.** VERIFIED:
`src/styles/noto-sans/index.scss` `@use`s exactly `base-400-normal`, `base-700-normal` and the same pair for
each CJK subset — no 500, no 600, no italic. A `font-weight: 600` therefore either synthesises or snaps to
the nearest bundled face, which would defeat §7.2's requirement that `ANY` and `all of` be the most salient
text in the block.

> **Two weights, both real: `400` and `700`.** Every place revision 1 said `500` or `600` now says `700`.
> Affected: §6.2 column 2 (`Name`), §7.2 (`ANY` / `all of`, diagnostic location lines), §7.5.

On the TV branch (`fonts.sized.scss`) and on `__USE_SYSTEM_FONTS__` builds the family is a system stack, where
400 and 700 are also the only weights that can be relied on. The choice is correct in all three branches.

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
| E6 | `GET /SmarterPlaylist/Schema` | M1 | S2 renderer + helper text + S3 template | `{ Members: MemberDescriptor[], Orders: string[], MediaTypes: string[], DefaultMaxItems: 1000, SeedTemplate: string }` — `SeedTemplate` is §7.5's pretty-printed new-definition JSON, served rather than hardcoded in the page so it cannot drift into naming a property reflection no longer finds |
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

`Kind` ∈ `Text` | `TextEnum` | `TextList` | `Number` | `Date` | `Boolean` | `Unsupported`.

**The derivation rule is rewritten.** Revision 1 keyed the `Date` row on a **name suffix** — "name ends with
`Date` or is `PremiereDate`". `UI-REVIEW.md` BLOCK C3 is correct that this classifies `DateCreated`,
`DateLastRefreshed`, `DateLastSaved` and `DateModified` as `Number`, directly contradicting §9. The heuristic
is gone. Rows are evaluated top-down; first match wins.

| # | Condition on the `Operand` property | `Kind` | `ValueControl` |
|---|---|---|---|
| 1 | Name is in the explicit set `DateMembers` (below) | `Date` | `date` |
| 2 | `typeof(bool)` | `Boolean` | `boolSelect` |
| 3 | `typeof(string)` **and** name is `MediaType` | `TextEnum` | `enumSelect` |
| 4 | `typeof(string)` | `Text` | `text` |
| 5 | Assignable to `IEnumerable<string>` | `TextList` | `libraryTypeahead` |
| 6 | `typeof(float)`, `double`, `int`, `long`, `short`, `decimal`, or a `Nullable<T>` of any of those | `Number` | `number` |
| 7 | **anything else — terminal fallback** | `Unsupported` | `none` |

`DateMembers` is a single `static readonly string[]` in the controller assembly, and it names exactly the
five members the engine normalises:

```csharp
// Must stay in lock-step with Engine._dateMembers.
private static readonly string[] DateMembers =
[
    nameof(Operand.PremiereDate),
    nameof(Operand.DateCreated),
    nameof(Operand.DateLastRefreshed),
    nameof(Operand.DateLastSaved),
    nameof(Operand.DateModified)
];
```

*DERIVED* — this is the same set as `Engine._dateMembers` (`QueryEngine/Engine.cs:33-40`). **A unit test must
assert the two arrays are equal**, so that adding a date member to one and not the other fails the build
rather than producing a date picker whose value the engine will not normalise.

`MediaType`'s enum is **`Jellyfin.Data.Enums.MediaType`**, fully qualified. `UI-REVIEW.md` FLAG B5 asked for
qualification and hypothesised two candidate types; a reflection probe against `Jellyfin.Controller` /
`Jellyfin.Model` 10.11.11 finds exactly one type named `MediaType` in the reachable assemblies, and
`BaseItem.MediaType` is of that type. Its members are `Unknown, Video, Audio, Photo, Book`. E6 emits
`MediaTypes` from `Enum.GetNames<Jellyfin.Data.Enums.MediaType>()`.

`DateRewritten` is `true` for **all five** `DateMembers` — defect D4 has landed (§4.4).

#### The `Unsupported` fallback, and why row 7 exists

`UI-REVIEW.md` FLAG D-F1 is right that §14's "zero front-end change" guarantee only ever held for `Name` and
`ClrType`, and that a member matching no row would get **no `Kind`** and fail silently one layer up. The
review's worked example is confirmed: a reflection probe shows `BaseItem.RunTimeTicks` is `long?`,
`ProductionYear` is `int?`, `Tags` is `string[]`, `OfficialRating` is `string`. Under revision 1's table a
`long` matched nothing.

Row 6 now covers the integral and nullable numeric cases, and row 7 catches everything else. A member with
`Kind: "Unsupported"` emits:

```json
{
  "Name": "SomeNewMember",
  "ClrType": "System.TimeSpan",
  "Kind": "Unsupported",
  "Operators": [],
  "ValueControl": "none",
  "DateRewritten": false,
  "Notes": "This property is filterable by the rule engine but the builder does not yet know how to edit it. Use the Advanced (JSON) tab."
}
```

Client contract for `Unsupported`, mandatory:
- The Rules tab **renders** the rule (member, raw operator, raw value) so the user can see it exists.
- The M4 builder shows the row read-only with the `Notes` string beside it, and does not offer the member in
  the "add a rule" picker.
- The validator does **not** reject it — the engine may well handle it fine. It emits warning W06 (§8.3).

**Two tests are required, and they are the mechanism that makes §14's claim true rather than aspirational:**

1. Every public property of `Operand` maps to a non-null `Kind`. (Trivially true given row 7 — the point is
   that it stays true, and that the test names the property when it regresses.)
2. No public property of `Operand` maps to `Kind: "Unsupported"`. This one is expected to **fail loudly**
   when N4 adds a member of an unhandled type, which is exactly the wanted behaviour: a build-time prompt to
   add a row, instead of a runtime blank.

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

### 4.4 Backend defects this UI exposes

Re-verified against `HEAD` (`98e4891`), not against the tree revision 1 was written on. Four of the eight
have landed and are recorded as **FIXED** so nobody re-fixes them; the rest are still open.

| ID | Status | Defect | Why the UI needs it |
|---|---|---|---|
| D1 | **OPEN — V1 blocker** | **M3 blast radius.** `RefreshAllPlaylists.ExecuteAsync:106-110` still loops `foreach (var dto in dtos) { … await RefreshPlaylistAsync(dto); }` with no `try`/`catch`. Anything thrown aborts the whole run. | The `try`/`catch` that fixes it *is* the capture point for `IRefreshStatusStore`. Without it there is no per-definition error to render, and the page would claim other playlists refreshed when they did not. |
| D2 | **FIXED** | `OperandFactory.GetMediaType` now writes `baseItem.Name ?? string.Empty` (`:34`), `baseItem.Album ?? string.Empty` (`:62`) and `baseItem.ContainingFolderPath ?? string.Empty` (`:73`). **Mechanism, stated correctly:** `MediaBrowser.Controller` is compiled **without** `<Nullable>enable</Nullable>`, so these members are *null-oblivious*, not annotated-nullable. A probe compiled against `Jellyfin.Controller` 10.11.11 under `#nullable enable` produces neither `CS8601` on read nor `CS8625` on assignment. Revision 1 called them "nullable", which implied the compiler would have flagged this; it never would have, which is why the fix had to be deliberate. | Warning **W04 is withdrawn** from §8.3 — it existed only while D2 was open. |
| D3 | **OPEN — V1 blocker** | **Store and filesystem are constructed, not injected.** `RefreshAllPlaylists`'s constructor still does `_plStore = new SmarterPlaylistStore(new SmarterPlaylistFileSystem(serverApplicationPaths));` (`:64`), and the field is typed as the concrete `SmarterPlaylistStore`, not `ISmarterPlaylistStore`. | The controller and the task must share one instance and one `BasePath`. Register `ISmarterPlaylistStore`, `ISmarterPlaylistFileSystem`, and `IRefreshStatusStore` via an `IPluginServiceRegistrator`, and change the field to the interface. |
| D4 | **FIXED** | `Engine.FixRules`/`FixRuleSets` are gone. `Engine.NormalizeRuleSets`/`NormalizeRules` (`:69-107`) normalise **all five** date members via `_dateMembers` (`:33-40`). | E6 sets `DateRewritten: true` for all five, and §9's date pickers are offered for all five. The "until fixed, emit Unix seconds for four of them" caveat is deleted. |
| D5 | **OPEN — V1 blocker** | `SmarterPlaylistFileSystem.GetSmarterPlaylistFilePath:39` still calls `Directory.GetFiles(...).First()` and throws `InvalidOperationException` when the file is absent. | E2/E4/E7 must return `404`, not `500`. |
| D6 | **PARTIALLY FIXED — the remainder is a V1 blocker** | `GetSmarterPlaylistPath` now rejects empty names and anything where `Path.GetFileName(playlistId) != playlistId` (`:70-73`), which closes traversal on the **write** path and closes the `BasePath/.json` case. **Still open:** the same unsanitised value reaches `GetSmarterPlaylistFilePath:39`, where it becomes a `Directory.GetFiles` **search pattern** with `SearchOption.AllDirectories` — so `*` or `?` in a route segment enumerates and returns an arbitrary file — and reaches `SmarterPlaylistStore.Delete:64` (M6's path). | Every endpoint taking `{fileName}` must reject anything not matching `^[A-Za-z0-9._-]{1,64}$`, and must additionally reject `.` and `..`, **at the controller boundary**, before any of the three call sites. This is a **new** security requirement introduced by this page and must not be skipped. |
| D7 | **OPEN — drop 2 blocker** | **`SaveAsync` minifies.** `SmarterPlaylistStore.cs:57` — `JsonSerializer.SerializeAsync(writer, smarterPlaylist)` with default options. | Out of scope to fix on disk (N6). But **E2 must return the JSON pretty-printed for display** regardless of on-disk format, otherwise the Advanced editor opens on a single unreadable line. See Section 14. |
| D8 | **FIXED** | **Date rules destroyed their own definition file, then broke permanently.** `Engine.FixRules` mutated `rule.TargetValue` in place on the DTO's own `Expression` objects, and `RefreshPlaylistAsync:133-134` writes that DTO back on first creation — so `"2020-07-01"` became `"1593561600"` on disk, and the next run's unconditional `DateTime.Parse("1593561600")` threw. `UI-REVIEW.md` identified this as the severe missing defect. It is closed by `98e4891`: `NormalizeRuleSets`/`NormalizeRules` build and return **new** `ExpressionSet`/`Expression` instances and never touch the input (`Engine.cs:69-107`, and the `<remarks>` at `:61-66` states the reason). | §7.2's "render the stored value **and** its human form" is therefore honest again — the stored value stays as the user wrote it. **A regression test is still required:** a definition with a `PremiereDate` rule must survive two consecutive refreshes with its file byte-identical. |
| D9 | **OPEN — not a V1 blocker, but E2 must avoid the trap** | `SmarterPlaylistStore.GetSmarterPlaylistAsync(Guid)` (`:18-23`) resolves `GetSmarterPlaylistFilePath(id.ToString())`, which globs for `{guid}.json` — but `SaveAsync` writes `{FileName}.json`. No file is ever named after a GUID, so this method can never match and always throws through D5's `.First()`. Nothing calls it today. | **E2 must resolve by on-disk file name and must never call `GetSmarterPlaylistAsync`.** It is the obvious method to reach for and it is a trap. Either delete it or give it an `[Obsolete]` with that sentence. Related to §4.5. |
| D10 | **OPEN — new, introduced by D6's fix** | With `GetSmarterPlaylistPath` now throwing `ArgumentException` on an empty `FileName`, and D1 still unfixed, a single definition with `"FileName": ""` that reaches the first-creation branch now **aborts the entire scheduled task** rather than writing `BasePath/.json`. The D6 fix converted a silent-corruption bug into a loud one, which is right — but D1 must land in the same release or the loudness takes every other playlist down with it. | Sequencing constraint, not a UI behaviour: **D1 and D6 ship together.** §8.2 E03 already blocks the UI from ever *writing* an empty `FileName`; D10 is about files that are already on disk. |

`Engine.NormalizeTargetValue`'s order — `DateTime.TryParse` **first**, numeric passthrough second, throw
otherwise (`Engine.cs:115-135`) — is the right order, and it is also the answer to `UI-REVIEW.md` FLAG E-F3.
See §8.2 E16 for the one residual case it leaves open and the measurement that corrects the review's premise.

---

### 4.5 `{fileName}` is the on-disk name, and it is the definition's identity

Six endpoints key on `{fileName}` and revision 1 never said what it meant. In this codebase the DTO's
`FileName` field and the actual name of the file on disk are **two different things that can diverge**, so
the ambiguity is real, not pedantic. *DERIVED*:

- `SmarterPlaylistFileSystem.GetSmarterPlaylistPath(userId, playlistId)` (`:68-76`) **ignores `userId`** and returns `Path.Combine(BasePath, $"{playlistId}.json")`.
- `SmarterPlaylistStore.SaveAsync` (`:52`) calls it as `GetSmarterPlaylistPath(smarterPlaylist.Id, smarterPlaylist.FileName)` — the **Id** is passed as the userId and discarded, and the **`FileName` field** decides the path.
- `GetAllSmarterPlaylistFilePaths` (`:53-56`) enumerates whatever `*.json` files exist, by their real names.

So a hand-authored `foo.json` containing `"FileName": "bar"` is written to `bar.json` on its first refresh
and `foo.json` is left in place: **two definitions where the user authored one**, both listed by E1, both
refreshing, both creating a Jellyfin playlist. This is not hypothetical — in V1 the Advanced JSON textarea is
the only editing surface, so `FileName` is freely editable on day one.

**Definitions, normative:**

1. **`{fileName}` is the on-disk file name with the `.json` extension removed.** It is the identity of a
   definition for the whole HTTP surface. E1 emits it, and it is what every other endpoint's route segment means.
2. **The `FileName` field inside the JSON is not an identifier.** It is a denormalised copy that this codebase
   uses only to compute a save path. The UI never treats it as identity and never displays it in preference
   to the route key.
3. **Invariant: `FileName` must equal `{fileName}`.** Every read and every write enforces it.

**Endpoint behaviour:**

| Endpoint | Behaviour |
|---|---|
| **E1** | For each file, compares the on-disk name against the parsed `FileName`. On mismatch, emits blocking diagnostic **E15** against that definition and sets status `Invalid` (§6.3 rank 2). The definition is listed, and it is listed as broken. Pre-existing divergent files become **visible instead of silently duplicating**. |
| **E2** | Resolves strictly by on-disk name. Returns E15 in `Diagnostics` when the body's `FileName` differs. Must not call `GetSmarterPlaylistAsync` (defect D9). |
| **E3** (create) | Takes `FileName` from the request body, validates it against `^[A-Za-z0-9._-]{1,64}$` (E03) and against collision (E04), and writes `{FileName}.json`. The two are equal by construction. |
| **E4** (update) | **Rejects with `400` and diagnostic E15 when the body's `FileName` differs from the route segment.** Message: `` This definition's file name is "{route}". Change it in the Advanced tab and the two would disagree. Rename is not supported — create a new definition and delete this one. `` E4 **never renames, never deletes, never writes a second file.** |
| **E7** (M6 delete) | Deletes `{fileName}.json` by on-disk name only. |

**Rejected alternative: implement rename in E4.** Rejected for V1. A correct rename is write-new →
verify → delete-old → reconcile the Jellyfin playlist that the old definition's `Id` points at, and it has
to be atomic against the 30-minute scheduled task reading the directory underneath it. That is a feature,
not a validation rule, and it is not what M2 is for. Delete-and-recreate is one extra step for a rare action
and it cannot corrupt anything. If rename is wanted later it gets its own endpoint and its own spec.

**Repair path for divergent files already on disk.** The UI does not auto-repair — that would be a silent
write to a user's file, which §11.4 and §4.3 both forbid. E15's rendered diagnostic names both names and
tells the user which one wins:

> `` This file is called foo.json but its FileName field says "bar". The scheduled task would write a second file called bar.json. Change FileName to "foo" in the Advanced tab, or rename the file on disk. ``

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

*Rejected alternative: a modal dialog.* **Revision 1's stated reason was wrong and is withdrawn.** It claimed
`dialogHelper` "is not verifiably reachable from a plain embedded config page"; in fact `Dashboard.dialogHelper`
is an exposed member of the global (`src/utils/dashboard.js:258`), so a modal *is* reachable and would come with
Jellyfin's own focus trap. The decision does not change, but it now rests on reasons that are true:

- The detail panel's whole job is to explain a row. Keeping the row on screen as context while the user reads
  a diagnostic that names `Group 2 › Rule 1` is worth more than the extra screen space a modal buys.
- The panel can be tall — a JSON editor plus a diagnostics list — and a modal on a tablet in portrait becomes a
  second scroll container inside a scrolling dashboard page, which is the one layout Jellyfin's own dashboard
  gets wrong most often.
- A modal is a second place focus can be lost, and §15 already has enough focus obligations.

`Dashboard.dialogHelper` remains available and is the right escape hatch if the panel ever needs to become a
dialog. The cost of the inline panel is that only one definition can be open at a time (enforced: opening a
row closes any other open row, after checking for unsaved changes).

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
| 2 | Name | flexible, min 14rem | Line 1: `Name` from the definition, **weight 700** (§3.6 — 500 is not a bundled face). If `Name` is empty, `(unnamed)` in `--jf-palette-text-secondary`. Line 2: `{fileName}.json` — the **on-disk** name (§4.5) — in `--jf-palette-text-secondary` at `0.9em`. The whole cell is the row's disclosure button (Section 11). |
| 3 | User | 10rem | The definition's `User` string, verbatim. If E1's diagnostics contain `UnknownUser` for this definition, append an `error` icon and, on the same line, `— no such user` in `--jf-palette-text-primary`. **The unknown-user determination is made server-side by E1** using `IUserManager.GetUserByName`, not by the client comparing against `getUsers()`, because the server's name-matching semantics are authoritative and were not verified to be case-sensitive or not. |
| 4 | Rules | 11rem | `{n} rules in {m} groups`. When `m > 1`, append ` · any group` — this is the only place OR-ness is visible in the collapsed row and it is not optional. Singular forms: `1 rule in 1 group`. When `m == 0`: `No rules` in `--jf-palette-text-secondary`. `title` attribute carries a one-line summary of group 1. |
| 5 | Items | 9rem | **The live count is always the primary value, and the last-run truncation fact is always separately labelled.** Line 1, `PlaylistState == Ok` → `PlaylistItemCount`, the live number, on its own. Line 2, only when the last run recorded `MatchedCount > AppliedCount` → `capped from {MatchedCount} at the last refresh` in `--jf-palette-text-secondary` at `0.9em`, `title="{MatchedCount} items matched; Max items is {MaxItems}"`. `PlaylistState == NotCreated` → `—` with `title="The Jellyfin playlist has not been created yet. It is created on the first successful refresh."`. `PlaylistState == Missing` → `Playlist missing` in `--jf-palette-text-primary` with an `error` icon. |
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

Row 1 gains a companion for §4.5: a definition whose on-disk name and `FileName` field disagree carries
blocking diagnostic **E15** and therefore lands on rank 2, `Invalid`.

Icon colour: statuses 1–3 use `color: var(--jf-palette-error-main, #c62828)` on the **icon only**. Statuses
4–5 use `--jf-palette-text-secondary`. Statuses 6–7 use inherited colour.

**There is no green anywhere.** Revision 1 justified this with a false claim — that Jellyfin ships no success
token. It does: `--jf-palette-success-main` is `#66bb6a` on dark (§3.3). The decision stands on two reasons
that are true:

- **It would not be safe anyway.** `success-main` measures **4.18:1** on the light scheme and `warning-main`
  measures **2.54:1** (§3.3). Neither can carry meaning without a text label, so adopting them buys nothing
  the icon + label pair does not already provide, and it costs a per-scheme contrast argument.
- **A table where most rows are green is noise.** The signal on this page is the small number of broken
  definitions. `OK` is the resting state and should recede, not compete.

`--jf-palette-warning-main` is deliberately **not** used for statuses 4–5 for the same contrast reason.

### 6.4 Page states

| State | Trigger | Rendering |
|---|---|---|
| **Loading** | Initial `pageshow`, and any full refetch of E1 | `Dashboard.showLoadingMsg()`, plus the table region replaced by `<p role="status">Loading playlist definitions…</p>`. **These are not two announcements.** `showLoadingMsg` toggles a class on a decorative MDL spinner `<div>` with no `role` and no live region (`src/components/loading/loading.ts`, VERIFIED), so the `role="status"` paragraph is the *only* thing a screen reader hears. Both are required: the spinner for sighted users, the paragraph for everyone else. No skeleton rows — Jellyfin has no skeleton primitive and hand-rolling one would not look native. |
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

Tabs: two `<button role="tab">` inside a `<div role="tablist" aria-label="Definition views">`. Do not use
`emby-tabs` — it is in the lazily-imported set (§3.2) and may never register on this page, and hand-rolled
tab buttons are a handful of ARIA attributes.

The tab pattern is specified completely, not two-thirds of the way (`UI-REVIEW.md` FLAG A4):

- **Roving `tabindex`.** The selected tab is `tabindex="0"`; every unselected tab is `tabindex="-1"`. Exactly
  one tab is in the page tab order, so Tab moves *into* and *out of* the tab strip rather than through each
  button. Without this the arrow-key contract is incoherent.
- **Manual activation.** Left/Right arrows and Home/End move **focus** between tab buttons and update
  `tabindex`/focus only. `aria-selected` and the visible panel change on **Enter, Space, or click** — never on
  arrow alone. This is the correct choice here for a concrete reason: the Advanced tab mounts a textarea and
  seeds it from E2's pretty-printed payload, and building that on a stray arrow key while the user is
  skimming is both slow and surprising.
- The selected tab carries `aria-selected="true"` and `aria-controls` pointing at the panel's id; the others
  carry `aria-selected="false"`.
- The tab panel is `role="tabpanel"` with `tabindex="0"` and `aria-labelledby` pointing back at its tab.
- Left/Right wrap around; Home/End jump to first/last.

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
- The words **ANY** and **all of** are rendered in **weight 700**. They carry the semantics and must be the most salient text in the block. Revision 1 said 600; `jellyfin-web` bundles Noto Sans at 400 and 700 only (§3.6), so 600 would synthesise or snap and would not reliably be the most salient text — defeating the requirement it was written to satisfy.
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

- Each diagnostic is a `<li>`. The location line (`Group 2 › Rule 1 — Operator`) is **weight 700**; the message is weight 400 in `--jf-palette-text-primary`.
- Diagnostics without a rule location render under the heading `Definition`.
- When the diagnostic carries a `Suggestion`, append a final line: `Did you mean "PremiereDate"?`
- Runtime failures recorded by `IRefreshStatusStore` render in their own sub-block headed `Last refresh failed at {absolute local time}`, with the exception type and message in a `<pre style="white-space: pre-wrap">`. This is the exact string that today only exists in the server log; it is reproduced verbatim, not paraphrased.
- JSON parse failures use `JsonException.LineNumber` and `BytePositionInLine` to render `Line 14, position 5: '}' expected.` and, when the Advanced tab is opened, the editor scrolls to and selects that line.

### 7.3 Tab: Advanced (JSON) — the V1 editing surface (drop 2)

**The element is a plain `<textarea>`, with no `is=` attribute.** Revision 1 specified
`<textarea is="emby-textarea">` and gave it no fallback; `UI-REVIEW.md` BLOCK B3 asked for one. Reading the
element settles it more decisively than a fallback would:

- `emby-textarea` is lazily imported by three feature components and by nothing on this page's load path, so
  on a cold load it would never upgrade at all (§3.2).
- If it *does* upgrade, `attachedCallback` sets `this.rows = 1` and attaches an unconditional `AutoGrow` that
  recomputes height from content (`src/elements/emby-textarea/emby-textarea.js:99-128`). A JSON editor wants a
  fixed, scrollable, user-resizable box. The two are in direct conflict, and which one wins would depend on
  whether some other dashboard page happened to load the module first — a non-deterministic layout bug.
- It also inserts its own `<label>` whose text is the `label` **attribute**, defaulting to the empty string.

So: a plain `<textarea>`, styled entirely by the page's scoped stylesheet. Concretely:

- `id="spDefinitionJson"`, with an explicit `<label for="spDefinitionJson">Definition JSON</label>` — visible, not `clipForScreenReader`.
- `spellcheck="false"`, `autocapitalize="off"`, `autocorrect="off"`, `wrap="off"`.
- `class="textarea-mono"` as progressive enhancement, **and** `font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace` in the scoped stylesheet, because `.textarea-mono` is absent on the TV font branch (§3.1).
- `min-height: 24em` desktop / `14em` below 640px (§12), `resize: vertical`, `overflow: auto`.
- Seeded from E2's pretty-printed JSON (see defect D7).
- `aria-describedby` points at a `.fieldDescription`: `Edits are validated against the plugin's rule engine before they are saved. Property names and operators are case-sensitive.`
- **No syntax highlighting, no code editor library.** That would mean a CDN or a bundled dependency, both forbidden by constraint 2.
- Live validation: debounced 600ms after typing stops, and immediately on blur → `POST /Validate`. Results render into the diagnostics block on the Rules tab **and** into a compact `<p role="status">` under the textarea: `Valid` / `{n} errors, {m} warnings`. Never validate per keystroke.
- **Tab always moves focus.** Indentation is the author's problem; trapping Tab in a textarea is a well-known keyboard trap. Stated explicitly so nobody "helpfully" adds it.
- The script that builds the diagnostics and status strings **must not use template literals** — see constraint 7. This is most likely to be violated right here, where string interpolation is most tempting.

**Judgement call, stated because it is contestable.** UX-RESEARCH §3 says to avoid a free-text DSL as the *primary* interface, and in drop 2 this textarea is the primary editing interface. It is shipped anyway because: (a) the README itself identifies "a page that simply lists and edits the JSON" as the correct cheapest first step; (b) it is strictly better than the status quo, which is filesystem access inside a container; (c) M2's synchronous validation removes the property/operator discoverability failure that made JSON authoring hostile; and (d) the Rules tab already occupies the position the M4 builder will take, so the builder replaces the *default* tab rather than being bolted on. *Rejected alternative: shipping M1 read-only with no editing at all.* Rejected because M2 ("validate on save") then has no save to validate — though note that §1.1's drop 1 does exactly this deliberately, as a *staged* release rather than a finished milestone.

**Named deviation from a research must-have.** `UI-REVIEW.md` FLAG E-F2 is right that revision 1 defended this
textarea against UX-RESEARCH §3 while silently skipping a second, separate deviation. Stating it now:

> UX-RESEARCH §4 flow 1 calls for the target-user selector to be **a dropdown of real Jellyfin users, not
> free text**, because it eliminates the "unknown user, logged and skipped" failure mode. Because the JSON
> textarea is V1's only editing surface, `User` is free text in V1. That is a deviation from a named
> must-have, and it is deferred to M4, not solved.

The mitigation is real but partial: E1 and E2 resolve `User` server-side through `IUserManager.GetUserByName`
and E06 blocks the save, so the failure moves from "silently skipped 30 minutes later" to "refused at save
time with the user's name in the message". That is most of the value.

*The review's suggested fix — promoting `User` out of the JSON into a dedicated `<select is="emby-select">`
above the tabs in V1 — is **declined**, with reasons:* (a) it is new V1 scope, and this revision is
explicitly not re-scoping; (b) it creates a second editing surface for one field that also still appears in
the JSON textarea below it, so the page must now define which one wins, what happens when they disagree, and
how the select behaves when the JSON is unparseable — that is more contract than the whole M4 builder row
for `User`; (c) `emby-select` is in the lazily-imported set (§3.2), so it would need its own degradation
contract. The right home for it is M4, where the builder owns the fields and the JSON tab becomes the
escape hatch rather than the source of truth. This is recorded in §17 as a judgement call, not an oversight.

### 7.4 Footer actions (drop 2)

| Button | Element | Enabled when | Behaviour |
|---|---|---|---|
| **Save** | `<button is="emby-button" class="raised button-submit">` | The document is dirty **and** the last validation returned zero blocking errors | `PUT /Definitions/{fileName}` with the raw text and `SourceHash`. On `200`: **write `Playlist definition saved.` into the page's own `role="status"` region (below), fire `Dashboard.alert('Playlist definition saved.')` for the visual toast, refetch E1, re-render the row, keep the panel open, and leave focus exactly where it is.** On `400`: render diagnostics into a `role="alert"` container and move focus to it. On `409`: see below. Do **not** call `Dashboard.processPluginConfigurationUpdateResult()` — these are not plugin configuration values and its localised "Settings saved" string would be misleading. |
| **Validate** | `raised button-alt` | Always | `POST /Validate`. Renders diagnostics into a `role="alert"` container. Used when the user wants a check without a debounce wait. |
| **Revert** | `raised button-alt` | Dirty | `Dashboard.confirm('Discard your unsaved changes to this definition?', 'Discard changes', function (ok) { … })`. On confirm, re-seed from the last E2 response. |
| **Delete** (M6) | `raised button-alt`, right-aligned, error-tinted border only | Always | Section 13.3 |

**Focus after a successful save: it does not move.** Revision 1 said "keep the panel open, move focus to the
panel heading" here while §15 said "focus stays where it is… never yank focus on success". These contradicted
each other (`UI-REVIEW.md` BLOCK A1). **§15 wins and the focus move is deleted.** A keyboard user who presses
Save is still working in the editor; relocating them to a heading is exactly the yank §15 forbids.

**The success announcement is page-owned, because the toast is silent.** `src/components/toast/toast.ts`
creates a bare `<div class="toast">` with no `role` and no `aria-live` (§3.5, VERIFIED). Relying on it, while
also correctly refusing to move focus, would leave a screen-reader user with **no confirmation at all** that
their save succeeded. So the panel carries, permanently:

```html
<p id="spSaveStatus" role="status" class="clipForScreenReader"></p>
```

The page writes into it on every terminal outcome of a save — `Playlist definition saved.`,
`Saved with 2 warnings.`, `Couldn't save cgp_grey.json — the server returned 500.` — independently of what
the toast does. It is emptied before each write so repeated identical messages still announce. This is
cheap, entirely under the page's control, and removes the dependency on undocumented platform behaviour.
(`UI-REVIEW.md` FLAG A3, upgraded from precautionary to required by measurement.)

**Concurrent-edit conflict (409).** E2 returns `SourceHash` — a hash of the file bytes as read. E4 requires
it and returns `409` when the file changed underneath. This exists because the Automator and Tinkerer
segments explicitly keep editing files by hand, and silently clobbering their work would be the worst
possible first impression for the page.

**E4's `409` response body is specified**, because revision 1 offered an `Overwrite anyway` button with no
hash to overwrite with — the client's hash is stale by definition, that is why it got the `409`
(`UI-REVIEW.md` FLAG C4). The body is:

```json
{
  "SourceHash": "<the file's current hash>",
  "RawJson": "<the file's current contents, pretty-printed>",
  "Diagnostics": [ /* validation of the current on-disk contents */ ]
}
```

UI: a `role="alert"` block — *"This file was changed on disk since you opened it. Someone else, or a hand
edit, may have modified it."* — with two buttons:

- **`Reload from disk`** — after a `Dashboard.confirm`, replaces the editor contents with the `409` body's
  `RawJson` and adopts its `SourceHash`. **No second round trip**, which is the point of returning the body.
- **`Overwrite anyway`** — re-PUTs the user's unchanged text with the `409` body's `SourceHash`. Because the
  server just handed that hash over, the retry is not a blind clobber: it is "I have seen that the file
  changed and I still want my version". If *that* PUT also returns `409`, the file changed twice during the
  exchange; render the alert again rather than looping automatically.

Both buttons are always enabled. Neither is the default — there is no auto-focus and no pre-selection,
because the safe choice depends on whose edit matters more and the page cannot know.

**Unsaved-changes guard.** Attempting to collapse the panel, open another row, or leave the page while dirty triggers `Dashboard.confirm('You have unsaved changes to "{Name}". Discard them?', 'Unsaved changes', …)`. There is no `beforeunload` handler — it is unreliable inside the dashboard's SPA routing and produces a browser-chrome dialog that does not look native.

### 7.5 S3 — New definition (drop 2)

Revision 1 put S3 in the information architecture, gave it an endpoint, two CTA labels and the empty state's
primary button, and then never specified it (`UI-REVIEW.md` BLOCK C1). Since it is the only creation path,
drop 2 cannot ship without it. Specified here to §7's depth.

#### Placement and shape

An inline panel rendered **above the table**, in flow, in the same visual idiom as the detail panel. Not a
modal, for the reasons in §5. It is mutually exclusive with an open detail panel: opening S3 collapses any
expanded row after the unsaved-changes guard, and vice versa.

Entry points, both of which do the same thing:
- `New smart playlist` in the list header (§13.1).
- `Create your first playlist` in the empty state (§6.4).

```
┌──────────────────────────────────────────────────────────────┐
│ New smart playlist                                [ Cancel ✕ ]│
│ ───────────────────────────────────────────────────────────── │
│ File name    [ my_playlist                    ]               │
│              Saved as my_playlist.json in /config/data/…      │
│                                                               │
│ Definition JSON                                               │
│ ┌───────────────────────────────────────────────────────────┐ │
│ │ {  … seed template …                                      │ │
│ └───────────────────────────────────────────────────────────┘ │
│ Edits are validated against the plugin's rule engine before   │
│ they are saved. Property names and operators are             │
│ case-sensitive.                                               │
│                                                               │
│ [ diagnostics block, when any ]                               │
│ ───────────────────────────────────────────────────────────── │
│ [ Create playlist ]  [ Validate ]          [ Cancel ]         │
└──────────────────────────────────────────────────────────────┘
```

#### Field set

Exactly two controls. Everything else lives in the JSON, for the same reason as §7.3 — a second editing
surface for a field that also appears in the JSON below it needs a precedence rule, and drop 2 is not the
place to introduce one.

| Field | Control | Contract |
|---|---|---|
| **File name** | `<input is="emby-input" type="text" id="spNewFileName">` with an explicit `<label for="spNewFileName">File name</label>` (§3.2 — never the `label` attribute) | Required. The definition's identity (§4.5). Validated client-side **for shape only** — `^[A-Za-z0-9._-]{1,64}$` — to disable the submit button early; the server re-validates authoritatively as E03/E04. A `.fieldDescription` below it renders `Saved as {value}.json in {BasePath}`, updating live as the user types, and `Saved as ….json in {BasePath}` when empty. |
| **Definition JSON** | Plain `<textarea id="spNewJson">` with `<label for="spNewJson">Definition JSON</label>`, styled exactly as §7.3 | Seeded from the template below. Same validation cadence as §7.3: debounced 600ms, immediate on blur. |

**The `FileName` field inside the JSON is not shown and not editable in S3.** The seed template omits it, and
E3 sets it from the File name input. This makes the §4.5 invariant true by construction for every definition
this page creates, and it removes the one way a user could create a divergent file on their first attempt.

#### Seed template

E6 supplies it (`GET /SmarterPlaylist/Schema` gains a `SeedTemplate` string), so it stays in lock-step with
the reflection-derived member list and cannot drift into naming a property that no longer exists. It is the
README's CGP Grey example reduced to a single group, with `Id` and `FileName` omitted:

```json
{
  "Name": "My smart playlist",
  "User": "",
  "ExpressionSets": [
    {
      "Expressions": [
        {
          "MemberName": "Directors",
          "Operator": "Contains",
          "TargetValue": "CGP Grey"
        }
      ]
    }
  ],
  "Order": { "Name": "Release Date Descending" },
  "MaxItems": 100
}
```

Three deliberate choices about the seed:

- **`User` is empty, not pre-filled with the current admin.** Pre-filling would make the most common mistake
  invisible, and §14/N5 forbids assuming the current admin is the target. Empty means E05 fires immediately
  and the very first diagnostic the user ever sees is `Choose the Jellyfin user this playlist is for.` — which
  is the single most useful thing this page can teach.
- **One group, one rule, and the rule is real.** A seed with an empty `ExpressionSets` would trip E07 and open
  on an error, teaching nothing. A seed with a plausible rule shows the shape of every field at once.
- **`MaxItems: 100`, not `0`.** `0` means "default 1000" and would open on warning W01. An explicit number
  demonstrates the field.

#### States

| State | Trigger | Rendering |
|---|---|---|
| **Pristine** | Panel opens | Seed template in the textarea, File name empty, `Create playlist` **disabled**, no diagnostics block. One `POST /Validate` fires on open so the diagnostics block is honest from the first frame — it will show E05 (`User` is empty). |
| **Editing, invalid** | Any validation returns ≥1 blocking error, or File name fails its shape check | `Create playlist` disabled. Diagnostics block rendered as §7.2. |
| **Editing, valid** | Zero blocking errors **and** File name matches `^[A-Za-z0-9._-]{1,64}$` | `Create playlist` enabled. Warnings, if any, still rendered. |
| **Submitting** | `Create playlist` pressed | Both controls and all three buttons disabled; button label → `Creating…`; `role="status"` announces `Creating playlist definition…`. |
| **Created** | E3 returns `201` | Panel closes. E1 refetched and the table re-rendered. The new row is **expanded** into its S2 detail panel, and focus moves to that panel's `<h3 tabindex="-1">` — this is a *new* location the user has not been to, so moving focus is correct and is not the §15 "yank on success" case. `role="status"` announces `Playlist definition {Name} created.` plus the toast. |
| **Rejected** | E3 returns `400` | Panel stays open with all input preserved. Diagnostics rendered into a `role="alert"` container; focus moves to it. |
| **Collision** | E3 returns `409` | See below. |
| **Server error** | E3 returns any other non-2xx | `role="alert"`: `Couldn't create {fileName}.json — the server returned {status}. Check the Jellyfin server log for SmarterPlaylist.` Input preserved, buttons re-enabled. |

#### `409` — file name collision

E3 returns `409` when `{FileName}.json` already exists. The body carries the colliding name:
`{ "FileName": "cgp_grey" }`. This is a **different** situation from E4's `409` (§7.4) — nothing the user
typed is at risk, they simply picked a taken name — so it is handled inline rather than with an alert block:

- The File name field gets `aria-invalid="true"` and its `aria-describedby` extended to a new error element.
- Error text, in `--jf-palette-text-primary`: `` A definition named "cgp_grey.json" already exists. Choose a different file name. ``
- Focus moves to the File name input and its contents are selected.
- `Create playlist` returns to disabled until the field changes.

Client-side pre-emption: the page already holds E1's list, so it *may* flag a known collision before
submitting, as a courtesy. It **must not** treat that check as authoritative — a file can appear on disk
between the list fetch and the POST, which is precisely why E3 owns the `409`.

#### Focus entry and exit

| Moment | Focus goes to |
|---|---|
| Panel opens | The **File name** input. It is the first field, it is required, and it is empty. |
| `Create playlist` succeeds | The new row's detail panel heading (see the Created state). |
| E3 returns `400` | The diagnostics `role="alert"` container. |
| E3 returns `409` | The File name input, contents selected. |
| Cancel, or Escape | The control that opened the panel — the header's `New smart playlist` button, or the empty state's `Create your first playlist` button. If that control no longer exists (the empty state is gone because a definition now exists), focus goes to the table's first row disclosure button. |

#### Cancel with unsaved changes

The panel is **dirty** once either control differs from its initial state — File name non-empty, or the
textarea differing from the seed template. Cancel, Escape, opening a row, or navigating away while dirty all
route through the same guard as §7.4:

```
Dashboard.confirm('Discard this new playlist definition?', 'Discard changes',
                  function (ok) { /* ok === true → close and reset */ });
```

When the panel is **not** dirty, Cancel and Escape close it silently — confirming a discard of nothing is
noise. As in §7.4 there is no `beforeunload` handler.

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
| **S3 panel opens** | `POST /Validate` once against the seed template | Informational — the diagnostics block is honest from the first frame, and the first thing shown is E05 (`User` is empty), which is the most useful thing the page can teach (§7.5) |
| Typing in S3's textarea, or blur | `POST /Validate`, debounced 600ms / immediate | Gates the `Create playlist` button, together with the File name shape check |
| **Save** / **Create playlist** press | Server re-validates inside E3/E4 and refuses the write | **Authoritative** |

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
| E15 | The body's `FileName` differs from the definition's on-disk name (§4.5) | On load: `` This file is called {onDisk}.json but its FileName field says "{body}". The scheduled task would write a second file called {body}.json. Change FileName to "{onDisk}" in the Advanced tab, or rename the file on disk. `` On E4: `` This definition's file name is "{route}". Rename is not supported — create a new definition and delete this one. `` |
| E16 | A `DateMembers` value parses as a bare integer **below `100000`** (§4.4) | `` "{TargetValue}" looks like a year, not a date. Dates are stored as Unix seconds, so this would mean {formatted} — a few hours after 1 January 1970. Write the full date as {TargetValue}-01-01. `` |

Note on **E16** — and a correction to `UI-REVIEW.md` FLAG E-F3. The review argued that the proposed D4 guard
would silently regress existing files, on the premise that `` DateTime.Parse("2020", InvariantCulture) ``
currently yields 1 January 2020. **That premise is false.** Measured on the .NET runtime:

| Value | `DateTime.TryParse(v, Invariant, AdjustToUniversal\|AssumeUniversal)` | `double.TryParse(v, Float, Invariant)` |
|---|---|---|
| `"2020-07-01"` | **true** → `2020-07-01T00:00:00Z` → `1593561600` | false |
| `"2020-07"` | **true** → `2020-07-01T00:00:00Z` | false |
| `"2020"` | **false** | true |
| `"1593561600"` | **false** | true |
| `"100000"` | **false** | true |

`DateTime.Parse("2020", InvariantCulture)` **throws `FormatException`** — it has never parsed as a year. So
`"2020"` was a hard failure before, not a working value, and nothing regresses.

Two things follow, and they are why the shipped implementation is right and the review's suggested
epoch-threshold guard is not needed in the **engine**:

1. `Engine.NormalizeTargetValue` tries `DateTime.TryParse` **first** and falls through to numeric passthrough
   (`Engine.cs:115-135`). Real dates always win; genuine Unix-second values always pass through; anything
   that is neither throws with a message naming the member. That ordering is the correct one and the review's
   concern about the opposite ordering does not apply to it.
2. There *is* a residual, and it is smaller than the review described: `"2020"` used to throw and now silently
   means **2020 seconds after the epoch** — 1 January 1970, 00:33:40Z. A `GreaterThan` rule against it matches
   the entire library. That is a plausible-wrong-answer, so it gets caught — but in the **validator**, where a
   wrong guess is a message rather than a behaviour change, not in the engine, where a threshold would be a
   magic number applied to values that are legal today. Hence E16, blocking, with the threshold stated once
   (`< 100000`, i.e. before 2 January 1970 — no real timestamp, and every four-digit year).

Required tests for `NormalizeTargetValue`: `"2020-07-01"` → `"1593561600"`; `"1593561600"` → unchanged;
`"2020"` → unchanged **and** rejected by E16; `"not a date"` → `ArgumentException` naming the member.

Note on **E14**: `SmarterPlaylist`'s constructor silently falls back to `NoOrder` for an unrecognised name. That is exactly the "plausible wrong answer with no error" pattern flagged in `ARCHITECTURE.md`. The engine's fallback stays (hand-edited files must keep working); the UI refuses to *write* one.

Note on **E08**: making an empty group a blocking error rather than a warning is a judgement call. *Rejected alternative: a warning.* Rejected because the consequence is an accidental playlist of the user's entire library up to `MaxItems`, the intent is almost never deliberate, and there is no cheap way to undo it once the scheduled task has run. A user who genuinely wants everything can write a rule that always passes.

### 8.3 Warnings — Save proceeds, warnings are shown

| ID | Condition | Message |
|---|---|---|
| W01 | `MaxItems` is 0 | `Max items is 0, so the default of 1000 applies.` |
| W02 | `Equal` or `NotEqual` on `CommunityRating` / `CriticRating` | `Exact equality on a rating rarely matches. Consider "is at least".` |
| W03 | `Contains` on a list member with a value not present anywhere in the target user's library | `No {member} in this library is exactly "{value}". Contains needs a whole exact element — use "matches regex" for partial matches.` — **V1-optional**, requires E9; ship it with M7 if E9 is not built for V1. |
| ~~W04~~ | ~~Any rule on `Album` or `FolderPath` while defect D2 is unfixed~~ | **Withdrawn.** D2 has landed (§4.4) — `OperandFactory` null-coalesces `Name`, `Album` and `FolderPath`. Do not implement this warning. |
| W05 | `MediaType` value not in `Enum.GetNames<Jellyfin.Data.Enums.MediaType>()` | `"{value}" is not a known media type. Known values: Unknown, Video, Audio, Photo, Book.` |
| W06 | A rule names a member whose `Kind` is `Unsupported` (§4.1) | `The rule builder does not yet know how to edit {member} ({ClrType}). The rule engine may still handle it. Edit it in the Advanced tab.` — non-blocking on purpose: the engine's capability is defined by CLR reflection, not by this page's table, so refusing the rule would make the UI *less* capable than the engine. |

Warnings are shown in the diagnostics block with a `warning` icon, and the save confirmation names them: `Saved with 2 warnings.`

### 8.4 Case sensitivity — must be stated in the UI

*DERIVED* from `Engine.BuildExpr`: `Enum.TryParse(r.Operator, out ExpressionType)` is case-sensitive by default, member lookup is `typeof(T).GetProperty(name)` (case-sensitive), and `string.Contains(string)` / `Collection<string>.Contains` are ordinal. Every free-text value control carries the `.fieldDescription` sentence: `Matching is case-sensitive.` M4's dropdowns make this moot for member and operator names but not for target values.

---

## 9. Rule builder — control by property type (M4)

Specified now because E6 must emit it from V1 and the V1 read-only renderer already consumes it. Derived from the actual `Operand` CLR types, not from the README table.

| `Operand` property | CLR type | `Kind` | Operators offered | Value control | Serialised `TargetValue` |
|---|---|---|---|---|---|
| `Name` | `string` | `Text` | `Equal`, `NotEqual`, `Equals`, `Contains`, `StartsWith`, `EndsWith`, `MatchRegex`, `NotMatchRegex` | `<input is="emby-input" type="text">` | verbatim |
| `Album` | `string` | `Text` | as above | text | verbatim. No warning — defect D2 has landed, so a null `Album` now projects as `""` rather than throwing on `Contains`/regex |
| `FolderPath` | `string` | `Text` | as above | text | verbatim. Same as `Album` |
| `MediaType` | `string` | `TextEnum` | `Equal`, `NotEqual`, `Equals` | `<select is="emby-select">` populated from E6's `MediaTypes` (`Unknown`, `Video`, `Audio`, `Photo`, `Book`) | the enum name verbatim |
| `Actors`, `Composers`, `Directors`, `Genres`, `GuestStars`, `Producers`, `Studios`, `Writers` | `Collection<string>` | `TextList` | `Contains`, `MatchRegex`, `NotMatchRegex` | **`Contains`** → typeahead over E9's library values (M7), free text until then. **regex** → text input with the hint `Matches if any single {member} matches.` | verbatim |
| `CommunityRating` | `float` | `Number` | `Equal`, `NotEqual`, `GreaterThan`, `GreaterThanOrEqual`, `LessThan`, `LessThanOrEqual` | `<input is="emby-input" type="number" step="0.1" min="0" max="10">` | **invariant** decimal — always `Number.prototype.toString()`, never `toLocaleString`; a comma decimal separator throws in `Convert.ChangeType` |
| `CriticRating` | `float` | `Number` | the six numeric operators | `<input is="emby-input" type="number" step="1" min="0" max="100">`, `.fieldDescription`: `Critic ratings are a percentage from 0 to 100.` | as above |
| `PremiereDate`, `DateCreated`, `DateLastRefreshed`, `DateLastSaved`, `DateModified` | `double` | `Date` | the six numeric operators | `<input is="emby-input" type="date">` | `YYYY-MM-DD`. `Engine.NormalizeRules` converts it to Unix seconds **for compilation only** and leaves the file's value alone (defect D8). All five are `DateRewritten: true`. |

**Why the ratings are split** (`UI-REVIEW.md` FLAG C5, confirmed): revision 1 gave both rating fields one
control with `max="10"`, which makes every realistic critic-rating rule unenterable. `CommunityRating` is the
0–10 user score; `CriticRating` is a **0–100 percentage**. VERIFIED against `jellyfin-web`'s own rendering —
`src/components/mediainfo/mediainfo.js:302-307` and `src/components/mediainfo/CriticRatingMediaInfo.tsx:17-19`
both branch on `CriticRating >= 60` to pick the "fresh" vs "rotten" style, which is the Rotten Tomatoes
percentage threshold. Both are `float?` on `BaseItem` (reflection probe, `Jellyfin.Controller` 10.11.11) and
both become non-nullable `float` on `Operand` via `GetValueOrDefault()`.

Note that `min`/`max` on a number input is a **hint, not a constraint** for our purposes: the server does not
reject an out-of-range rating, because a library could in principle carry one, and W02 already covers the
common mistake. The attributes exist to make the spinner useful and the intent obvious.
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
2. **One panel at a time.** Expanding a row collapses any other expanded row **and the S3 panel**, after the unsaved-changes guard (7.4, 7.5). Opening S3 likewise collapses any expanded row. There is never more than one editing surface on screen.
3. **Escape** collapses the open panel — detail or S3 — after the unsaved-changes guard, and returns focus to the control that opened it.
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
| **<640px** | Three columns: **Status**, **Name**, **Actions**. Name carries name, filename, user, rules summary, item count, and last refresh as stacked lines at `0.9em` in `--jf-palette-text-secondary`, separated by `--sp-2`. |

**On the folded secondary text at `<640px`.** `UI-REVIEW.md` BLOCK A2 was right to challenge this — at that
breakpoint essentially the entire informational payload of the page is rendered in `--jf-palette-text-secondary`
at roughly 14px, which is body text and needs 4.5:1, and revision 1 had asserted rather than measured it, in
the wrong theme. Measured across all six colour schemes (§3.3), `--jf-palette-text-secondary` is **8.59:1 on
dark and 5.39:1 at worst (light)**. It passes as body text everywhere, so **the fold keeps secondary text**
and no promotion to `--jf-palette-text-primary` is required. The two constraints that come with that:

- The definition **name** stays `--jf-palette-text-primary` at weight 700. The hierarchy at this breakpoint
  comes from weight, size and order — colour is doing the least of the work, deliberately.
- Nothing may stack additional opacity on the token (§3.3 consequence 2). The 5.39:1 figure has no headroom
  for a second alpha multiply.

*Rejected alternative: CSS-stacked "card" rows via `display: block` on `tr`/`td`.* Rejected because setting `display: block` on table elements strips their implicit ARIA roles in several browsers, silently breaking the table for screen-reader users on exactly the devices where the layout is most compressed.
*Rejected alternative: a horizontally scrolling table wrapper.* Rejected because horizontal scroll inside a vertically scrolling dashboard page is poor on touch and hides the Actions column, which is the column most likely to be wanted.

Additional requirements:
- All interactive targets are **≥44×44 CSS px**, enforced in the scoped stylesheet (`min-height: 2.75rem; min-width: 2.75rem`) rather than assumed from `emby-button`'s own sizing.
- The detail panel is single-column below 900px: tabs stack above the tab body, and the footer buttons wrap with `gap: var(--sp-2)` rather than shrinking.
- The Advanced textarea is `min-height: 24em` on desktop and `min-height: 14em` below 640px, so the on-screen keyboard does not leave a two-line editing slot.
- **No horizontal scrolling of the page at any width down to 320px.** Revision 1 said 360px here and 320px in §15; WCAG 1.4.10 (Reflow) requires 320px, and 360 is the weaker number an implementer would have tested against. Both now say 320px.
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

- The radio group is `<fieldset>` + `<legend class="clipForScreenReader">What should happen to the Jellyfin playlist?</legend>`.
- **`emby-radio` is VERIFIED and may be used** — `src/elements/emby-radio/emby-radio.js`,
  `document.registerElement('emby-radio', { prototype: EmbyRadioPrototype, extends: 'input' })` at `:71-74`.
  Revision 1 named it without evidence, which broke §3.2's own gate (`UI-REVIEW.md` BLOCK B1); it is now in the
  §3.2 inventory with a citation.
- **The markup shape is not optional.** `attachedCallback` does `labelElement.querySelector('span')` and then
  `labelTextElement.classList.add('radioButtonLabel')` **with no null check** (`emby-radio.js:38-42`), so a
  parent that is not a `<label>`, or a `<label>` with no `<span>`, throws a `TypeError` during upgrade. The
  required shape, exactly:

  ```html
  <label>
    <input type="radio" is="emby-radio" name="spDeletePlaylist" value="keep" checked>
    <span>Keep the Jellyfin playlist as a static list</span>
  </label>
  ```

- Per §3.2, `emby-radio` is lazily imported and may not upgrade. Unupgraded, this is a native radio inside a
  native label — fully functional and correctly labelled. The `is=` attribute is enhancement only.
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
| **N4** new `Operand` properties / orders | E6 is reflection-derived (Section 4.1) and `Orders` is a server-supplied list, so a new property or order appears in the UI with zero front-end change. **This is the single most important forward-compatibility constraint in this spec.** *The claim is now true rather than overstated:* `Name` and `ClrType` were always genuine reflection, but `Kind`, `Operators` and `ValueControl` come from a table, and revision 1's table had no terminal row — a `long` such as `RunTimeTicks` matched nothing and got no `Kind` at all. §4.1 now has row 6 covering integral and nullable numerics, row 7 as an `Unsupported` terminal fallback that degrades **visibly** (W06) rather than silently, and two tests: every `Operand` property maps to a non-null `Kind`, and none maps to `Unsupported`. The second test is designed to fail at build time when N4 lands, prompting a new row instead of a blank control. |
| **N5** per-user self-service | Not achievable through a config page at all (constraint 4). V1 must nonetheless keep `User` as a first-class field on the definition and must never assume "the current admin" is the target user, so that a future non-config-page surface can reuse the same file format and the same endpoints. |
| **N6** pretty-printed JSON on disk | E2 must return pretty-printed JSON **for display** regardless of on-disk format (defect D7), so the Advanced editor is usable without N6. When N6 lands, E2's formatting step becomes a no-op — no UI change. |

---

## 15. Accessibility requirements

Non-negotiable. Each is testable.

**Structure and labelling**
- The definitions list is a real `<table>` with `<caption class="clipForScreenReader">`, `<thead>`, and `<th scope="col">`. No ARIA grid pattern — a static table needs none, and a half-implemented grid is worse than a table.
- Every control has an accessible name. Icon-only buttons pair a `title` with a `<span class="clipForScreenReader">` that **includes the definition name**, so names are unique across rows: `Edit CGP Grey`, `Delete CGP Grey`.
- All Material icons are `aria-hidden="true"`. They never carry meaning alone.
- **Every input is associated with an explicit `<label for>` in the markup.** Revision 1 permitted `emby-input`'s `label` **attribute** as the primary mechanism. That is unsafe: the attribute only becomes a `<label>` if the element upgrades, and `emby-input` is lazily imported and may never register on this page (§3.2, VERIFIED from the import graph). An unupgraded `<input is="emby-input" label="File name">` has **no accessible name at all**. The `label` attribute may be set additionally, for visual consistency where the element does upgrade, but it is never the labelling mechanism. Placeholder text is never a substitute for a label.
- Helper text is linked with `aria-describedby`; a field with an error also gets `aria-invalid="true"` and its `aria-describedby` extended to include the error element's id.

**Keyboard**
- Every action reachable by pointer is reachable by keyboard in DOM order. No positive `tabindex` anywhere.
- Row disclosure is a `<button aria-expanded aria-controls>`, operable with Enter and Space.
- Tabs: **roving `tabindex`** (selected tab `0`, all others `-1`), Left/Right arrows move focus between tabs and wrap, Home/End jump to first/last, and activation is **manual** — Enter, Space or click. Arrows never change the visible panel. The tab panel is `role="tabpanel" tabindex="0" aria-labelledby`. Full contract in §7.1.
- Escape closes the open detail panel, the S3 panel, and the delete confirmation.
- **No keyboard traps.** Explicitly: Tab inside the Advanced textarea moves focus and does not insert a tab character (7.3).
- Visible focus indication is never removed. If a custom control needs one, use
  `outline: 2px solid var(--jf-palette-text-primary, #fff); outline-offset: 2px`.
  **Not `--jf-palette-primary-main`.** Revision 1 specified `#00a4dc`, which measures 5.70:1 on the dark
  theme but only **2.33:1 on the light scheme and 2.86:1 on appletv** (§3.3) — below WCAG 1.4.11's 3:1 for
  focus indicators. `text-primary` is 13.58:1 at worst across all six schemes. `UI-REVIEW.md` A-P3 passed
  this token, but measured dark only; the finding does not generalise and the token is changed.

**Focus management**
- Expanding a row moves focus to the panel's `<h3 tabindex="-1">` heading.
- Collapsing a row returns focus to the disclosure button that opened it.
- Opening S3 moves focus to its File name input; closing it returns focus to whatever opened it (§7.5).
- After a save that produces errors, focus moves to the diagnostics container.
- **After a successful save, focus stays exactly where it is. Never yank focus on success.** §7.4 previously
  contradicted this by moving focus to the panel heading; that instruction is deleted and this rule is
  authoritative. The one apparent exception is not one: creating a definition via S3 moves focus into the
  *newly created* detail panel, which is a new location the user asked to be taken to, not a relocation
  within a surface they were already working in.
- Revealing the delete confirmation moves focus to its heading; cancelling returns focus to the Delete button.

**Announcement**
- The list summary line (`12 definitions · 2 with errors`) is `aria-live="polite"`. This is the only thing announced on page load — per-row diagnostics are **not** live regions at load time, which would flood a screen reader with a dozen alerts.
- Diagnostics rendered **in response to a user action** (Validate, Save, conflict) are in a `role="alert"` container that is emptied and repopulated so the change is announced.
- **Every success is announced by a page-owned `role="status"` region, never by the toast.** `Dashboard.alert`'s toast form renders a bare `<div class="toast">` with no `role` and no `aria-live` (`src/components/toast/toast.ts`, VERIFIED), so it is visual-only. The panel's `#spSaveStatus` (§7.4) and S3's equivalent are what actually announce. Emptied before each write so a repeated identical message still announces.
- The "Run refresh now" progress is announced through a `role="status"` region: `Refreshing…` then `Refresh finished.`
- The loading state's `<p role="status">` is likewise the only loading announcement — `Dashboard.showLoadingMsg()` toggles a decorative spinner with no live region (§6.4).

**Colour and contrast**
- No information is conveyed by colour alone. Every status is icon + text (Section 6.3).
- `--jf-palette-error-main` measures **2.90:1 on dark and 2.70:1 at worst (wmc)** and is therefore restricted to decorative borders and icon fills; error message text uses `--jf-palette-text-primary` (Section 3.3).
- Secondary text uses `--jf-palette-text-secondary`, which is `rgba(255,255,255,0.7)` on the four dark schemes and `rgba(0,0,0,0.6)` on the two light ones — **8.59:1 on dark, 5.39:1 at worst.** It is safe as body text. Do not add opacity on top of it; there is no headroom for a second alpha multiply. (Revision 1 quoted `rgba(0,0,0,0.87)` here, which is light-mode text-*primary* — a value that appears in neither role in the dark theme this page is normally read in.)
- The page must be usable at 200% browser zoom and at a **320px** CSS viewport width without horizontal scrolling. §12 now states the same number.

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
11. **Localisation.** V1 ships English literals, centralised in one object at the top of the script block.
    The mechanism is now **VERIFIED**, and it is more constrained than revision 1 implied:
    `globalize.translateHtml` (`src/lib/globalize/index.js:246-272`) substitutes every `${Key}` in the page
    against `defaultModule()` — the **core `jellyfin-web` dictionary** — and returns the bare key on a miss
    (`:218-236`). So `${Key}` works only for strings jellyfin-web already ships. There is no mechanism for a
    plugin to supply its own dictionary to this path. Localising this page's own copy therefore means either
    contributing keys upstream or doing the substitution in the plugin's own controller before the HTML is
    served. Both are out of scope. **See constraint 7** — this same function is why the page must contain no
    JavaScript template literals, which is a V1 constraint, not a localisation one.

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
| 1.1 | A stated cut line: read-only drop 1, editing drop 2 | Ship V1 as one undifferentiated release | The research designed M1–M3 to be splittable and revision 1 offered no way to; naming the cut is cheaper than discovering it under deadline |
| 3.2 | Plain `<textarea>` for the Advanced editor | `<textarea is="emby-textarea">` with a degradation contract | The element is lazily registered *and*, if it does upgrade, forces `rows = 1` + `AutoGrow` onto a fixed-height JSON editor. Which behaviour you get depends on what else the session loaded — a non-deterministic layout bug |
| 3.2 | `emby-collapse` removed from the permitted list | Use it for the detail panel | No graceful degradation: unupgraded it is an inert `<div>` with no disclosure affordance at all |
| 3.6 | Two font weights, 400 and 700 | 400 / 500 / 600 | `jellyfin-web` bundles Noto Sans at 400 and 700 only; 600 would synthesise and would not reliably be the most salient text §7.2 requires |
| 4.5 | `{fileName}` is the on-disk name; E4 refuses a rename | Implement rename in E4 | A correct rename is write→verify→delete→reconcile-the-playlist, atomic against a running scheduled task. That is a feature with its own endpoint, not a validation rule |
| 4.5 | Surface divergent `FileName` as a blocking error | Auto-repair the field on load | A silent write to a user's file, which §11.4 and §4.3 both forbid |
| 5 | Inline panel (reason restated) | Modal via `Dashboard.dialogHelper` | `dialogHelper` *is* reachable — revision 1's reason was false. The real reasons are row-as-context, tablet-portrait scroll containers, and one fewer place to lose focus |
| 6.3 | No green, and no `warning-main` either | Adopt the MUI success/warning tokens now that they are known to exist | They measure 4.18:1 and 2.54:1 on the light scheme, so they cannot carry meaning without a text label — which the icon + label pair already provides |
| 7.3 | `User` stays in the JSON in drop 2 | Promote `User` to a dedicated `<select>` above the tabs | New V1 scope; creates a second editing surface for a field that is still in the JSON below it, requiring a precedence rule; `emby-select` needs its own degradation contract. M4 owns it |
| 8.2 E16 | Catch bare-integer date values in the **validator** | Add an epoch-plausibility threshold to `Engine.NormalizeTargetValue` | A magic number in the engine changes the meaning of values that are legal today; a validator rule is a message, not a behaviour change |
| 15 | Focus outline uses `--jf-palette-text-primary` | `--jf-palette-primary-main` (`#00a4dc`) | 2.33:1 on the light scheme — below 1.4.11's 3:1 for focus indicators. 5.70:1 on dark is not the whole picture |

---

## 18. Open questions for the maintainer

1. **Does the UI become the sole source of truth, or does hand-edited JSON stay a first-class parallel path?** This spec assumes **parallel**, which is why it specifies the `SourceHash` conflict flow (7.4), keeps the definitions folder path permanently visible (6.1), and treats a file appearing on disk as a first-class way to create a definition. If the answer is "UI is authoritative", the conflict flow can be simplified but the Automator segment loses its workflow.
2. **Is the in-memory status store's restart gap acceptable**, or should sidecar status files ship in V1? This spec says acceptable; Section 4.3 documents the upgrade path.
3. ~~**Should defect D4 (`FixRules` covering all five date members) ship in V1?**~~ **Answered by the code.**
   D4 and D8 both landed in `98e4891`. All five date members normalise, and normalisation no longer mutates
   the caller's DTO. §9 offers a date picker for all five.
4. **Should the release be split at §1.1's cut line?** New question. Drop 1 (read-only list, server-side
   validation, per-definition error surfacing) is genuinely shippable and needs no write path, no concurrency
   model and no create flow. Drop 2 adds editing. This spec covers both and does not decide.
5. **Does the executor accept the "no JavaScript template literals" constraint (constraint 7), or should the
   plugin pre-substitute?** The alternative is for `SmarterPlaylistController` to serve the page HTML itself
   rather than relying on `IHasWebPages`' embedded-resource path, doing its own token substitution first.
   That is more machinery than a lint rule, but it would remove a footgun that produces silent wrong strings.
   This spec assumes the lint rule.

---

## 19. Review disposition

Every finding in `.planning/ux/UI-REVIEW.md`, mapped. Nothing is silently dropped.

### Blocking (8 of 8 addressed)

| ID | Disposition | Where |
|---|---|---|
| **A1** post-save focus contradiction | **Fixed** — the focus move is deleted from §7.4; §15 is authoritative and says so explicitly | §7.4, §15 |
| **A2** unmeasured `text-secondary` contrast | **Fixed** — measured across all six colour schemes with alpha compositing. Dark **8.59:1**, worst case **5.39:1** (light). Above 4.5:1 everywhere, so §12's `<640px` fold keeps secondary text. Revision 1's `rgba(0,0,0,0.87)` was light-mode text-*primary* and was worthless as evidence. **Bonus finding the review's A-P3 missed:** `--jf-palette-primary-main` is 2.33:1 on the light scheme, so §15's focus outline token is changed to `text-primary` | §3.3, §12, §15 |
| **B1** `emby-radio` unverified | **Fixed** — VERIFIED at `src/elements/emby-radio/emby-radio.js:71-74`, added to §3.2 with a citation, and §13.3 now specifies the exact `<label>`+`<span>` markup its `attachedCallback` requires to avoid a `TypeError` | §3.2, §13.3 |
| **B2** "complete emitted set" vs `paperChannel` | **Fixed, and the underlying claim was worse than the review suspected.** Revision 1's two cited files do not exist. The tokens come from MUI 6.4.12 driven by `src/themes/themes.ts:125-134` (`cssVarPrefix: 'jf'`) over `src/themes/defaults.ts`. The real dark-scheme set is **307** palette declarations; `paperChannel` does exist (`32 32 32`); `#202020` is re-cited to `defaults.ts`; "complete" is withdrawn. Also corrected: success and warning tokens **do** exist, contra revision 1 | §3.3 |
| **B3** `emby-textarea` trusted without fallback; `label` attribute over-claimed | **Fixed, more strongly than requested.** Reading the import graph shows **every** `emby-*` element except `emby-button` is lazily registered and may never upgrade — a general rule, not a one-element caveat. `emby-textarea` is dropped entirely for the editor: if it *does* upgrade it forces `rows = 1` + `AutoGrow` onto a fixed-height JSON editor. The `label` attribute is real (verified in source) but upgrade-dependent, so explicit `<label for>` is now mandatory | §3.2, §7.3, §15 |
| **C1** S3 unspecified | **Fixed** — new §7.5: placement, field set, seed template (E6-supplied, with the reasoning for each choice), six states, `409` collision handling, focus entry/exit table, cancel-with-unsaved-changes | §7.5 |
| **C2** `{fileName}` ambiguous | **Fixed** — new §4.5: `{fileName}` is the on-disk name without extension and is the definition's identity; the JSON's `FileName` is a denormalised copy; E4 rejects a mismatch with `400`/E15 and never renames; E1 surfaces pre-existing divergent files as blocking errors on load; S3 omits `FileName` from the seed so new definitions cannot diverge | §4.5, §6.3, §7.5, §8.2 E15 |
| **C3** `Kind` name-suffix heuristic | **Fixed** — the heuristic is gone. An explicit `DateMembers` set, tested for equality against `Engine._dateMembers`. Plus D-F1's safety net: row 6 covers integral and nullable numerics (`long`, `int?`, `decimal`…), row 7 is a terminal `Unsupported` fallback that degrades visibly via W06, and two tests make N4 fail at build time | §4.1, §14 |

### Flags (14 of 14 addressed)

| ID | Disposition | Note |
|---|---|---|
| **A3** silent save confirmation | **Fixed, and upgraded from precautionary to required.** `toast.ts` creates a bare `<div class="toast">` with no `role` and no `aria-live` — VERIFIED. The toast **is** silent. §7.4 adds a page-owned `#spSaveStatus` `role="status"` | §7.4, §15 |
| **A4** tab pattern two-thirds specified | **Fixed** — roving `tabindex` stated explicitly, manual activation chosen, wrap and Home/End defined, `aria-labelledby` added | §7.1, §15 |
| **A5** 360px vs 320px | **Fixed** — both say 320px, with the WCAG 1.4.10 reason | §12, §15 |
| **B4** `Dashboard.confirm` signature | **Fixed** — VERIFIED callback-style at `src/utils/dashboard.js:208-214` on the 10.11 branch. The suggested `Promise.resolve(...)` wrapper is **declined as unnecessary**: the function returns `undefined` and the shape is confirmed, so the wrapper would be defensive code against a hypothesis that has been tested | §3.5 |
| **B5** `Enum.GetNames<MediaType>()` unqualified | **Fixed, and the premise corrected.** A reflection probe against `Jellyfin.Controller`/`Jellyfin.Model` 10.11.11 finds exactly **one** reachable type named `MediaType` — `Jellyfin.Data.Enums.MediaType`, which is `BaseItem.MediaType`'s type. There were not two candidates. Fully qualified everywhere, with its five members named | §4.1, §9, §8.3 W05 |
| **C4** `Overwrite anyway` has no hash | **Fixed** — E4's `409` body is specified as `{ SourceHash, RawJson, Diagnostics }`, so `Overwrite anyway` re-PUTs with the server's current hash and `Reload from disk` needs no second round trip | §7.4 |
| **C5** `CriticRating` is not 0–10 | **Fixed** — the row is split: `CommunityRating` 0–10 step 0.1, `CriticRating` 0–100 step 1 with a `.fieldDescription`. VERIFIED against `jellyfin-web`'s own `CriticRating >= 60` fresh/rotten threshold | §9 |
| **C6** Items cell conflates two numbers | **Fixed** — the live `PlaylistItemCount` is always the primary value on line 1; the truncation fact moves to a separate secondary line explicitly labelled `capped from {n} at the last refresh` | §6.2 |
| **C7** double loading announcement | **Declined, with evidence.** There is no double announcement. `Dashboard.showLoadingMsg()` toggles a class on a decorative MDL spinner `<div>` with no `role` and no live region (`src/components/loading/loading.ts`, VERIFIED), so it announces nothing. The `<p role="status">` is the *only* announcement and removing it would leave screen-reader users with silence. Both are kept, and §6.4 now records why so this is not re-flagged | §6.4 |
| **D-F1** `Kind` half-derived; `long` falls through | **Fixed, all three parts.** (1) Terminal `Unsupported` row with `Operators: []` and a `Notes` string; (2) two server-side tests, including one that fails when any `Operand` property maps to `Unsupported`; (3) the `Date` name-suffix heuristic replaced by an explicit member set. Row 6 additionally widened to `long`/`short`/`decimal`/`Nullable<T>`, since `RunTimeTicks` is `long?` and `ProductionYear` is `int?` (reflection-verified) | §4.1, §14 |
| **E-F1** V1 is large, no cut line | **Fixed** — §1.1 states the cut explicitly rather than quietly re-scoping. Drop 1 is E1+E2+E6+D1+D3+D5+D6 with a read-only panel; drop 2 adds E3/E4/E5/D7, the editor, the footer, S3 and the `409` flow. Very close to the review's proposed cut; D4 is absent because it has already landed | §1.1 |
| **E-F2** free-text `User` is an unnamed deviation | **Partly fixed, partly declined — stated as such.** The deviation from UX-RESEARCH §4 flow 1 is now named in §7.3 in as many words. Promoting `User` to a dedicated `<select>` in V1 is **declined**: it is new V1 scope, it creates a second editing surface for a field still present in the JSON below it (needing a precedence rule and an unparseable-JSON behaviour), and `emby-select` would need its own degradation contract. M4 owns it | §7.3, §17 |
| **E-F3** proposed D4 guard regresses existing files | **Superseded by the code change, and the review's premise is corrected.** `DateTime.Parse("2020", InvariantCulture)` **throws `FormatException`** — measured. It has never yielded 1 January 2020, so no legal value changes meaning. The shipped `NormalizeTargetValue` tries `DateTime.TryParse` first and falls through to numeric, which is the correct ordering and is not the one the review analysed. The genuine residual — `"2020"` used to throw and now silently meant 1970-01-01T00:33:40Z — **has since been fixed in the engine** (`NormalizeTargetValue` rejects numeric values in 1000-9999 naming the property and telling the user to write a full date; covered by `EngineTest.BareYearIsRejectedRatherThanReadAsATimestamp`). This spec originally placed the guard in validator error **E16** only; that is **not sufficient**, because hand-edited JSON is a first-class path and bypasses the UI entirely — the same reason this spec specifies a `SourceHash` conflict flow. E16 is retained as a UI-side early warning so the user sees the problem before saving, not as the only line of defence | §4.4, §8.2 |
| **Pillar 4** typography: three weights, 500/600 may not exist | **Fixed, and the suspicion was right.** `src/styles/noto-sans/index.scss` `@use`s only `*-400-normal` and `*-700-normal` for every subset. Two weights declared: **400 and 700**. Every 500/600 in revision 1 is changed | §3.6, §6.2, §7.2 |
| **Pillar 5** spacing: no scale declared | **Fixed** — a six-step scale (`--sp-1` … `--sp-6`, 4/8/16/24/32/48) declared in §3.6 and made the only permitted source of spacing, with content sizing and the 44px touch floor explicitly exempt | §3.6, §12 |

### Findings the review recorded as correct — preserved unchanged

A-P1 (the `#c62828` arithmetic — independently reproduced at 2.90:1), A-P2 (the 1.4.1 / 1.4.11 / 1.4.3
mitigation), A-P4 (keyboard and announcement commitments), B-P1 (all DERIVED claims about this repo),
B-P2 (NEEDS VERIFICATION items carry real fallbacks), C's state coverage and the §9 control mapping,
D-P1 (E6 must be reflection-derived), D-P2 (M5/M6/M7 residual work), D-P3 (N1/N3/N5 obligations),
E-P1 (non-goals), E-P2 (backend cost stated), and pillars 1, 2, 3 and 6. None of these was weakened.
Where a preserved conclusion rested on a false premise — §6.3's "no success token", §5's `dialogHelper`
claim — the conclusion is kept and the premise is replaced with a true one, in the open.

### Also noted: the working tree now builds

`UI-REVIEW.md` item 11 recorded that the tree did not build (`CA1823`, unused `_dateMembers`). That was an
in-progress edit; `_dateMembers` is now consumed by `NormalizeTargetValue` (`Engine.cs:117`), and the test
project references `Jellyfin.Controller`/`Jellyfin.Model` **without** `ExcludeAssets=runtime`, so tests can
load Jellyfin types — which is what makes the §4.1 reflection tests writable at all.

---

*UI design contract authored 2026-07-25, revised the same day against `UI-REVIEW.md`. Platform facts are
cited to `jellyfin/jellyfin-web@release-10.11.z` commit `35c0793`, to `@mui/material@6.4.12` driven with
Jellyfin's own theme options, or to a compiled probe against `Jellyfin.Controller` 10.11.11. Items that
could not be verified are labelled NEEDS VERIFICATION with a stated fallback. Three claims in revision 1
were asserted and turned out to be false; they are corrected in place with the correction visible rather
than quietly overwritten.*
