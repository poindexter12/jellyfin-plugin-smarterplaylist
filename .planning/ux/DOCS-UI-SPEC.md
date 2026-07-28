# DOCS-UI-SPEC: Smarter Playlist recipes site

**Status:** draft
**Date:** 2026-07-28
**Surface:** `docs/` — published at `https://poindexter12.github.io/jellyfin-plugin-smarterplaylist/`
**Build:** GitHub Pages legacy Jekyll build, source `main` `/docs`
**Scope:** structure, layout, styling, navigation. **Not** copy — the nine recipes' prose is settled.
**Consumers:** planner, executor, ui-checker

This is a different surface from `.planning/ux/UI-SPEC.md`, which specs the in-Jellyfin plugin
configuration page. Nothing here applies to that page and nothing there applies here.

---

## 0. How to read this document

Every claim about the platform is labelled:

- **VERIFIED** — measured from the live build or read from GitHub's own published dependency
  manifest, this session. Source given.
- **NEEDS VERIFICATION** — plausible, not confirmed. A fallback is given. Do not rely on it.
- **DESIGN DECISION** — a choice made by this spec, not a platform fact.

### 0.1 Verification method

| Instrument | What it establishes |
|---|---|
| `curl` of `https://poindexter12.github.io/jellyfin-plugin-smarterplaylist/recipes/` (37,055 bytes) | The actual emitted HTML: which stylesheets load, whether Rouge highlighting is on, which token classes appear, the heading anchor ids |
| `curl` of the served `/assets/css/style.css?v=ab214b81…` (9,569 bytes) | Cayman 0.2.0's complete compiled ruleset — every colour, every breakpoint |
| `https://pages.github.com/versions/` | GitHub Pages' allowlist of plugin gems and theme gems, with versions |
| WCAG 2.1 relative-luminance arithmetic over sRGB hex, computed this session | Every contrast ratio quoted below |

**Residual gaps, stated rather than hidden:**

1. I did **not** verify which allowlisted plugins GitHub Pages auto-enables versus which require an
   explicit `plugins:` entry. This matters because calling an unloaded Liquid tag (`{% seo %}`) is a
   hard build failure, not a silent no-op. §8.6 resolves this by using **no optional plugins at all**,
   so the question never arises.
2. I did not run a Jekyll build locally (no Ruby toolchain in this session). Every Liquid construct
   used below is core Jekyll (`relative_url`, `site.title`, `page.title`, `content`) — no plugin tags.

---

## 1. Measured baseline — what is actually wrong

Not opinion. These are the served bytes.

**VERIFIED — Cayman 0.2.0's own rules, from the served stylesheet:**

| Rule | Value | Consequence |
|---|---|---|
| `.main-content h1…h6` | `font-weight: normal; color: #159957` | **Headings are the same weight as body text.** This is the root cause of "nine recipes look identical" — the only signal separating a recipe title from a paragraph is hue and ~1.5× size, and the hue is a green that fails contrast (below). |
| `.main-content h2` | `margin-top: 2rem; margin-bottom: 1rem` | 32px above a heading, 16px below, and paragraphs are `margin-bottom: 1em`. A recipe title is barely more separated from the previous recipe's Gotcha than from its own body. No grouping. |
| `.main-content pre` | `background-color:#f3f6fa; border: solid 1px #dce6f0; color: #567482` | |
| `.highlight` | `background-color: #f8f8f8` | **Two different code backgrounds nested inside each other** — the Rouge wrapper is `#f8f8f8`, the `pre` inside it is `#f3f6fa`. |
| `.main-content pre` | `font: 1rem …` then `.main-content .highlight pre { font-size: 0.9rem; line-height: 1.45 }` | Code sits at 0.9 × 16px = 14.4px while prose is 1.1rem = 17.6px, and both are the same weight and near-identical greys. This is the "code blocks and prose run together" complaint, precisely. |
| `@import url("https://fonts.googleapis.com/css?family=Open+Sans…")` | render-blocking third-party font | plus a `preconnect` to `fonts.gstatic.com` in `<head>` |
| No `prefers-color-scheme` block anywhere | — | Dark mode is not a tweak away; **every colour in Cayman is a hardcoded literal, with no custom properties**. See §2. |

**VERIFIED — contrast ratios of the shipped site (computed this session):**

| Element | Colours | Ratio | WCAG 2.1 AA |
|---|---|---|---|
| Heading text | `#159957` on `#ffffff` | **3.67:1** | **FAILS** (needs 4.5:1; only ≥24px/≥18.66px-bold qualify for 3:1, and Cayman sets headings to `font-weight: normal`, so h3–h6 fail outright) |
| Body text | `#606c71` on `#ffffff` | 5.41:1 | Passes |
| Code text | `#567482` on `#f3f6fa` | 4.60:1 | Passes by 0.1 |
| Rouge string literal | `#dd1144` on `#f3f6fa` | 4.57:1 | Passes by 0.07 |
| Footer credits | `#819198` on `#ffffff` | 3.26:1 | **FAILS** |
| Rouge whitespace token | `#bbbbbb` on `#f3f6fa` | 1.77:1 | N/A — carries no information |

**VERIFIED — syntax highlighting is already on.** The served HTML contains 10 `class="highlight"`
blocks (one per fenced block: nine recipes plus "Combining these") with Rouge token spans present:
`nl` (object keys), `s2` (string values), `p` (punctuation), `mi` (integers), `w` (whitespace).
**No highlighting library needs to be added.** What is missing is a colour scheme worth looking at
and a dark variant. This kills the most tempting wrong move — see §9.1.

**VERIFIED — `relative_url` resolves correctly on this project site.** The served page links
`/jellyfin-plugin-smarterplaylist/assets/css/style.css`, which is Cayman's layout emitting
`{{ '/assets/css/style.css' | relative_url }}`. The Pages build therefore supplies a working
`site.baseurl` without it being declared in `_config.yml`. All asset paths in this spec use
`relative_url` on that evidence.

**VERIFIED — heading anchor ids are kramdown-generated from heading text.** The live page carries
`#watch-a-franchise-in-broadcast-order`, `#night-shuffle-that-skips-what-you-just-watched`, and the
other seven. Since this spec changes **no heading text**, all nine ids survive unchanged. This is the
governing constraint on §3.

---

## 2. Verdict on the theme

### DESIGN DECISION: Own the layout and the stylesheet outright. Keep `theme: jekyll-theme-cayman` in `_config.yml` as an inert anchor. Add no Sass, no remote theme, no plugin.

Concretely: create `docs/_layouts/default.html` and `docs/assets/css/site.css`. Jekyll resolves a
local `_layouts/default.html` in preference to the theme gem's, so Cayman's layout never renders. Our
layout links **only** `site.css`, so Cayman's `/assets/css/style.css` is still built but never
requested — zero bytes, zero effect. Cayman ends up contributing nothing but a valid `theme:` key.

**Why the key stays rather than being deleted:** I have not verified what the Pages legacy build does
when `theme:` is absent (whether it falls back to a default theme, or none). Leaving a valid,
allowlisted value there costs nothing and removes an unverified variable. This is a
**NEEDS VERIFICATION** avoidance, deliberately.

### Justification against each constraint

| Constraint | How this satisfies it |
|---|---|
| GitHub Pages legacy build only | `_layouts/` and `assets/` are core Jekyll. No gem, no plugin, no `Gemfile` change, nothing outside the allowlist. |
| No build step, no npm, no Actions | One hand-written `.css` file served as a static asset, one hand-written `.js` file, one Liquid layout. Nothing compiles. |
| Plain-Markdown readability on github.com | The layout and CSS live outside the Markdown entirely. `recipes.md` gains **zero** new syntax. |
| Degrades without JS | The layout and CSS deliver the header, nav, hierarchy, code styling and dark mode with no JS at all. JS is additive only (§4, §5). |
| Survives neglect | No external dependency can move under it: no Google Fonts, no CDN, no remote theme fetch, no gem upgrade, no Sass compiler. The only upstream that can change is the Pages Jekyll version itself, and we use only its oldest, most stable features. |

### What it costs — stated plainly

1. **You now own ~230 lines of CSS and ~90 lines of JS.** Nobody upstream will fix a bug in them.
   Mitigation: they are dependency-free and small enough to read in one sitting.
2. **You lose Cayman's responsive header and button styles.** Both are being deliberately discarded
   anyway (§6.1) — the giant gradient hero is 5rem of padding above the fold on every page of a site
   whose whole value is the payload below it.
3. **No upstream accessibility fixes.** Offset by the fact that Cayman 0.2.0 currently ships two AA
   failures (§1) and has not been released since 2019 (**NEEDS VERIFICATION** — inferred from the
   version pin at 0.2.0 across the whole `jekyll-theme-*` family; treat as "unmaintained enough that
   waiting for a fix is not a plan").
4. **Two new files to keep in your head.** Total docs surface goes from 3 files to 6.

### Alternatives considered and rejected

**Switch to another allowlisted theme.** VERIFIED — the complete list is: `architect`, `cayman`,
`dinky`, `hacker`, `leap-day`, `merlot`, `midnight`, `minimal`, `modernist`, `primer`, `slate`,
`tactile`, `time-machine` (all 0.2.0 except `primer` 0.6.0). **Not one of them has a dark mode, a
navigation chrome, or a table of contents.** `midnight` and `hacker` are permanently dark, which is
worse than permanently light — it ignores the reader's OS preference in the other direction. So a
theme swap cannot satisfy requirement 5 (dark mode) or requirement 3 (navigation) under any
selection. **Rejected on capability, not taste.**

**`remote_theme` to Just the Docs.** VERIFIED — `jekyll-remote-theme 0.4.3` is on the allowlist, and
Just the Docs is a genuine fit on paper: sidebar nav, search, a dark scheme. Rejected on three
grounds: (a) it introduces a **build-time network fetch of a third-party repository** — if that fetch
fails or the tag moves, your docs stop building, which is the exact opposite of "survives neglect";
(b) its dark mode is a **static config choice** (`color_scheme: dark`), not `prefers-color-scheme`
auto-following, so getting the behaviour actually wanted still requires custom JS on top; (c) it is a
multi-thousand-line theme with a large config surface, adopted to lay out **two pages**. The
maintenance asymmetry is absurd. If it were ever adopted, it **must** be pinned
(`remote_theme: just-the-docs/just-the-docs@v0.10.1`) — an unpinned `remote_theme` is a live
dependency on someone else's `main`.

**Override Cayman by importing it and patching.** The conventional
`docs/assets/css/style.scss` → `@import "{{ site.theme }}";` → overrides pattern. Rejected: Cayman
has **no custom properties**; every colour is a literal inside a specific selector
(`.main-content h1`, `.main-content pre`, `.main-content blockquote`…). A dark mode therefore
requires re-declaring essentially every rule at equal-or-higher specificity inside a media query. The
override sheet would be *larger* than writing 230 lines from scratch, and it would carry Cayman's
Google Fonts `@import` and hero markup along with it. Writing less code by writing all of it is the
unusual-but-correct answer here. **Rejected on volume, verified by reading all 9,569 bytes.**

**Any Sass.** Rejected. `jekyll-sass-converter 1.5.2` is a deprecated libsass generation. We use
nothing Sass provides that CSS custom properties do not. Plain `.css` served statically also skips
the front-matter requirement and one whole class of build failure.

---

## 3. Page architecture

### DESIGN DECISION: The nine recipes stay in one file — `docs/recipes.md`. Do not split.

**Why, in priority order:**

1. **Deep links.** `README.md:12` already points the world at
   `https://poindexter12.github.io/jellyfin-plugin-smarterplaylist/recipes/`, and nine
   `#anchor` ids are live (§1, VERIFIED). Splitting moves eight of the nine to new paths. Fragment
   identifiers are **never sent to the server**, so no redirect can rescue them — not
   `jekyll-redirect-from` (allowlisted at 0.16.0, and irrelevant here), not anything else GitHub
   Pages offers. Fragment-to-page routing is only possible with client-side JS, which would mean an
   inbound deep link that works only if JS runs. Splitting therefore either breaks live links or
   makes them JS-dependent. Both unacceptable.
2. **The GitHub file view.** One `recipes.md` reads as one coherent cookbook in the repo browser.
   Nine files in a `_recipes/` collection read as nine fragments plus an index that, on github.com,
   is a list of links to files whose front matter GitHub renders as a metadata table. Strictly worse.
3. **Ctrl-F.** For a copy-paste cookbook, "search the whole thing at once" is a feature, not a
   fallback. Nine pages destroys it and nothing on the allowlist gives search back.
4. **Nine is not a scale problem.** Splitting is a response to a scale that does not exist yet. §3.4
   records the trigger for revisiting.

### 3.1 Resulting page structure

Two pages, unchanged:

```
/                 index.md    — what it is, install, orientation, two gotchas
/recipes/         recipes.md  — nine recipes + combining
```

Within `/recipes/`, the visual architecture is **one scroll, hard-sectioned**:

```
┌─ site header (sticky, from layout) ────────────────────────┐
│  Smarter Playlist       Recipes · Reference ↗              │
└────────────────────────────────────────────────────────────┘
   Recipes                                    ← page title, from layout
   Complete playlist definitions for…         ← intro prose, from markdown

   ┌──────────────────────────────────────────┐
   │ [Broadcast order] [Night shuffle] [Docs] │  ← the EXISTING bullet list,
   │ [Recently added] [Rewatch] [Kid-safe] …  │    restyled by CSS into chips
   └──────────────────────────────────────────┘

  ┌ sidebar ┐  ┌─ recipe card ──────────────────────────────┐
  │ ▸ Broad │  │ ## Watch a franchise in broadcast order    │
  │   Night │  │ Every Star Trek series interleaved…        │
  │   Docs  │  │ ┌──────────────────────────── [Copy] ──┐   │
  │   …     │  │ │ { "Name": "All of Star Trek", …      │   │
  │         │  │ └──────────────────────────────────────┘   │
  │ (JS)    │  │ ▸ Why it works. …                          │
  │         │  │ ▸ Gotcha. …                                │
  └─────────┘  └────────────────────────────────────────────┘
```

### 3.2 How "cards" happen without touching the Markdown

The Markdown already contains the exact structure needed. `recipes.md` separates every recipe with a
`---` horizontal rule (lines 23, 50, 83, 112, 136, 163, 191, 220, 244, 268, 285). **The `<hr>` is the
card boundary.** The CSS in §8.3 does not draw cards as boxes — it uses the `hr` to draw a hairline
rule and `h2` to open a heavily-weighted, generously-spaced band. Result: nine unmistakable sections,
zero Markdown changes, and on github.com the `---` still renders as the same divider it does today.

**DESIGN DECISION: no boxed cards.** A boxed card around a recipe would nest a boxed code block
inside a boxed card inside a boxed page. Rule-and-space separation reads better at this density and
costs nothing in dark mode.

### 3.3 The index list becomes a chip row

The existing bullet list at `recipes.md:13–21` stays exactly as written. CSS turns it into a wrapped
row of chips. Selector (§8.3): `.page-recipes .prose > ul:first-of-type`.

**Stated fragility:** this keys off "the first `<ul>` on the Recipes page". If someone later inserts a
different list above it, the styling lands on the wrong list. It degrades to a normal bullet list —
ugly, not broken. Accepted in exchange for zero Markdown pollution. The alternative was a kramdown
IAL (`{: .recipe-index}`), which **renders as literal visible junk on github.com** and is therefore
disqualified by the hard constraint.

The body class comes from the layout, keyed on `page.title`, so `recipes.md`'s front matter is not
touched either.

### 3.4 When to revisit splitting

Revisit only when **all three** hold: more than ~20 recipes; a demonstrated need to filter by
category; and a willingness to accept that pre-split anchors become JS-dependent. Until then, one
file.

---

## 4. Code-block treatment

The JSON blocks are the payload. They get the strongest visual treatment on the page.

### 4.1 Syntax highlighting — mechanism

**Already solved. Do not add a highlighter.** VERIFIED: kramdown + Rouge 3.30.0 runs server-side in
the Pages build and already emits token spans. The work is a colour scheme for the tokens actually
produced by the JSON lexer, in both schemes. Token classes observed in the live output, plus the
handful the recipes could yet produce:

| Class | Token | Appears as |
|---|---|---|
| `.nl` | object key | `"Name"`, `"ExpressionSets"` |
| `.s2` | double-quoted string value | `"MatchRegex"`, `"^Star Trek"` |
| `.p` | punctuation | `{ } [ ] : ,` |
| `.mi` / `.mf` | integer / float | `1000`, `50` |
| `.kc` | keyword constant | `true`/`false`/`null` — not present today, styled defensively |
| `.w` | whitespace | carries no information; must not be tinted (§1 shows Cayman tints it at 1.77:1) |
| `.err` | lexer error | styled loudly on purpose — a visible red block is a bug report |

**DESIGN DECISION: the object key is the loudest token.** In these recipes the reader scans for
`MemberName`, `Operator`, `TargetValue` — the keys are the structure. Keys get the accent hue; string
values get a warm secondary; punctuation recedes.

### 4.2 Visual distinction from prose

Four simultaneous signals, so the distinction survives greyscale, dark mode, and low vision:

1. **Surface** — code sits on `--surface-code`, distinctly darker than the page in light mode and
   distinctly lighter in dark mode.
2. **Border** — a full 1px `--border` box, `border-radius: 8px`.
3. **Bleed** — at ≥64em code blocks extend 16px past the prose measure on both sides. Long JSON lines
   get more room, and the block visibly breaks the text column.
4. **Font** — a real monospace stack at 14px/1.55 against 17px/1.65 prose (§6.2). Cayman's 14.4px
   code is retained as a size but given weight, colour and surface separation it currently lacks.

Horizontal overflow: `overflow-x: auto` with `-webkit-overflow-scrolling: touch`. Do **not** soft-wrap
JSON — the aligned columns in the M\*A\*S\*H and Best-of-the-90s recipes are load-bearing for
readability and wrapping destroys them. Long lines scroll. Accepted.

### 4.3 Copy to clipboard — mechanism under the no-build constraint

**Client-side only, ~40 lines of dependency-free JS, injected at runtime.** There is no other option:
adding a button server-side requires either a Jekyll plugin (not allowlisted, no such plugin exists on
the list) or Liquid/HTML inside the fenced blocks (renders as junk on github.com — disqualified).

Behaviour contract:

| Aspect | Contract |
|---|---|
| Trigger | On `DOMContentLoaded`, for every `div.highlight` in `main` |
| DOM change | Wrap in `<figure class="code">`; append `<button class="copy" type="button">`. Never inject **inside** `<pre>` — the button text must not land in the copied payload |
| Source text | `figure.querySelector('pre').textContent`, with a single trailing newline stripped |
| Label | `JSON.parse` the text. Object with a `Name` key → **"Copy definition"**. Anything else → **"Copy"**. This is how the "Combining these" fragment and the install URL avoid being mislabelled as complete definitions, **with zero markdown annotation** |
| API | `navigator.clipboard.writeText`. Fallback: hidden `<textarea>` + `document.execCommand('copy')` |
| Success | Label swaps to "Copied", `aria-live="polite"` on the button, reverts after 1600 ms |
| Failure | Label swaps to "Press ⌘C" and the block's text is selected via `Range`, so the user is one keystroke from done |
| Focus | Visible `:focus-visible` ring. Reachable by keyboard in DOM order, immediately before its block |
| Hit area | 32px tall, 12px horizontal padding; `@media (pointer: coarse)` raises to 44px |
| No-JS | No button. The `<pre>` is selectable text exactly as today. **Reading and copying a recipe never requires JS** |

`navigator.clipboard` requires a secure context. GitHub Pages is HTTPS-only, so this is satisfied in
production; the `execCommand` fallback covers `file://` previews and older browsers.

---

## 5. Navigation

Four layers. The first two require no JS.

**5.1 Sticky site header (layout, no JS).** Full-width, 56px, `position: sticky; top: 0`. Left: site
title linking to `/`. Right: `Recipes` and `Reference ↗` (the README). Current page marked with
`aria-current="page"` set from `page.title` in Liquid. This is the "no way back" fix at the site
level — every scroll position has the site nav in view.

**5.2 Chip row (CSS on existing Markdown, no JS).** §3.3. Sits directly under the intro. Nine
targets, visible without scrolling on desktop, wrapping on mobile. This is the primary
"which recipe do I want" affordance and the no-JS answer to in-page navigation.

**5.3 Back-to-contents button (layout + CSS, no JS; JS refines).** A `position: fixed` bottom-right
anchor to `#top`. Default state in CSS is **visible** — that is the no-JS guarantee. JS adds
`.is-scrolled` to `<html>` past 600px and CSS hides the button above that threshold, so on a
JS-enabled browser it appears only once you have actually left the top. 44 × 44px, respects
`prefers-reduced-motion`.

**5.4 Sticky sidebar TOC with scroll-spy (JS enhancement).** At ≥64em only. Built at runtime from
`main h2` — **derived, never hand-maintained**, so it cannot drift from the content. Active section
tracked with `IntersectionObserver`. When JS installs it, `<html>` gets `.has-toc` and CSS hides the
chip row at that breakpoint so the two indexes never both show.

**5.5 Heading anchor links (JS enhancement).** A `#` link revealed on hover/focus of each `h2`,
href-ing that heading's existing kramdown id. Makes the deep links §3 is protecting actually
discoverable. Marked `aria-hidden="true"` plus an `aria-label` so screen readers get one useful
announcement rather than nine "number sign"s.

**Deliberately absent: a search box.** Nothing on the GitHub Pages allowlist provides server-side
search, and a client-side index over two pages is worse than Ctrl-F, which already works because §3
kept everything in one file.

---

## 6. Typography and colour

### 6.1 What is discarded

Cayman's gradient hero (`5rem 6rem` of padding, a `linear-gradient(120deg, #155799, #159957)`, and
`.btn` styles for buttons this site does not have). Replaced with a 56px sticky bar plus a page title
in the content column. **Recovers roughly 200px of above-the-fold space on every page**, on a site
whose entire job is to get the reader to a JSON block.

Google Fonts is removed: the `@import` inside the stylesheet, the `<link rel=preconnect>`, and the
`<link rel=preload>`. Three fewer third-party round trips, one fewer thing that can break, and one
fewer party told who reads your docs.

### 6.2 Type scale — 4 sizes, 2 weights

| Token | Size / line-height | Weight | Used for |
|---|---|---|---|
| `--fs-title` | 28px / 1.2 | 700 | `h1` page title, `h2` recipe titles |
| `--fs-lead` | 20px / 1.4 | 400 | intro paragraph on each page; `h3` |
| `--fs-body` | 17px / 1.65 | 400 | all prose |
| `--fs-small` | 14px / 1.55 | 400 | code blocks, chips, sidebar, footer, copy button |

Two weights only: **400 and 700**. No 600 — system font stacks synthesise it inconsistently. The
recipe titles jump from Cayman's `font-weight: normal` to **700**; combined with §6.4 spacing this is
the single largest fix to problem 1.

`h2` at 28px/700 sits above the WCAG large-text threshold, so its colour has headroom — but §6.3
gives it full body-text contrast anyway rather than spending that headroom.

Stacks (no webfonts, no downloads):

```
--font-sans: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto,
             "Helvetica Neue", Arial, sans-serif;
--font-mono: ui-monospace, SFMono-Regular, "SF Mono", Menlo, Consolas,
             "Liberation Mono", monospace;
```

Measure: `--measure: 68ch` on the prose column. Code blocks bleed 16px wider (§4.2).

### 6.3 Colour — 60 / 30 / 10, both schemes

Palette derived from Jellyfin's own brand blue (`#00A4DC`) so the docs read as part of that ecosystem
rather than as a stock GitHub page. **VERIFIED: `#00A4DC` itself is 2.78:1 on a near-white page and
must never carry text in light mode.** It is used only as a border/marker accent there; the light
scheme's text-bearing accent is a darkened derivative.

| Role | Share | Light | Dark |
|---|---|---|---|
| `--bg` — page surface | **60%** | `#fbfcfd` | `#101418` |
| `--surface-code` — code blocks, chips, sidebar | **30%** | `#eef2f6` | `#171d23` |
| `--border` | — | `#dbe3ea` | `#252d35` |
| `--text` | — | `#1b2126` | `#e6edf3` |
| `--text-muted` | — | `#59646e` | `#93a1ad` |
| `--accent` — links, active TOC marker, focus ring | **10%** | `#07698f` | `#58c8f0` |
| `--accent-edge` — 3px rules, chip hover edge, code-block left edge | (within the 10%) | `#00a4dc` | `#00a4dc` |

**Accent is reserved for exactly these and nothing else:** inline and block links; the active
sidebar-TOC item's left marker; the `:focus-visible` ring; the JSON object-key token; the 3px left
edge on code blocks. Recipe headings do **not** get accent — they get `--text`, because when nine
headings are all accent-coloured the accent stops meaning "actionable". This is a direct correction of
Cayman, which colours every heading green.

Second semantic colour: **none.** This site has no destructive actions and no error states. Adding a
red would be decoration. `.err` (§4.1) uses `--text` on a red-tinted surface and is a build-quality
signal, not a UI state.

**VERIFIED contrast, computed this session:**

| Pair | Light | Dark |
|---|---|---|
| text on page | **15.82:1** | **15.65:1** |
| muted on page | 5.89:1 | 7.00:1 |
| accent on page | 5.63–5.97:1 | 9.63:1 |
| muted on code surface | 5.14:1 | 6.42:1 |
| code plain text on code surface | 14.45:1 | 13.20:1 |

All pass AA for normal text; everything except muted also passes **AAA** (7:1). Compare §1: this
removes both existing AA failures.

**Rouge token colours — VERIFIED contrast on `--surface-code`:**

| Token | Light | ratio | Dark | ratio |
|---|---|---|---|---|
| `.nl` key | `#0b5a8a` | 6.56:1 | `#7fd4f5` | 10.22:1 |
| `.s2` string | `#9b2c2c` | 6.69:1 | `#f0a8a0` | 8.75:1 |
| `.mi`/`.mf` number | `#7a4bbd` | 5.23:1 | `#c9aef5` | 8.78:1 |
| `.p` punctuation | `#414b55` | 7.90:1 | `#9fb0bd` | 7.62:1 |
| plain / `.w` | `#1b2126` | 14.45:1 | `#dbe4ec` | 13.20:1 |

Every token passes AA. Cayman's shipped scheme has two tokens sitting within 0.1 of the AA floor.

### 6.4 Spacing — 4px base

Scale: **4, 8, 12, 16, 24, 32, 48, 64.** No value off this scale anywhere.

The hierarchy fix is concentrated here:

| Relationship | Value | vs Cayman |
|---|---|---|
| `hr` (recipe boundary) → next `h2` | **48px** | 32px |
| `h2` → its intro line | **8px** | 16px |
| paragraph → its code block | **16px** | 16px |
| code block → "Why it works" | **24px** | 16px |
| last paragraph → next `hr` | **48px** | 16px |

The asymmetry is the whole point: **8px inside a recipe's title group, 48px between recipes.** A
6× ratio makes the nine sections read as nine objects at a glance instead of one grey wall. Cayman's
current ratio is 2× and inverted at the boundary.

### 6.5 Dark mode

**Mechanism: `@media (prefers-color-scheme: dark)` re-declaring custom properties on `:root`. Nothing
else.** Roughly 12 lines of CSS. No JS, no toggle, no `localStorage`, no flash of wrong theme, no
persistence bug. Follows the OS, which for a Jellyfin admin is already dark.

The layout also declares `<meta name="color-scheme" content="light dark">` so form controls,
scrollbars and the pre-paint background match, avoiding a white flash before CSS applies.

**DESIGN DECISION: no manual light/dark toggle.** A toggle needs JS, a storage key, a
render-blocking inline script to prevent FOUC, and a control in the header. That is four moving parts
to override a preference the reader has already expressed at OS level. Named in §9.5.

---

## 7. Accessibility contract

- Skip link to `#content`, first focusable element, visible on focus. (Cayman has one; preserve it.)
- Every interactive element has a visible `:focus-visible` ring: `2px solid var(--accent)` with a 2px
  offset. Never `outline: none`.
- Sticky header uses `scroll-margin-top: 72px` on `h2` so an anchor jump does not land the heading
  underneath the bar. **This is required, not polish** — without it, all nine live deep links from
  §3 land on a heading hidden behind the header.
- Sidebar TOC is a `<nav aria-label="On this page">` containing a real `<ul>` of real anchors.
- Copy button: `<button type="button">`, `aria-live="polite"`, real text label (not an icon alone).
- `@media (prefers-reduced-motion: reduce)` removes all transitions and sets `scroll-behavior: auto`.
- Touch targets ≥44px on `(pointer: coarse)`.
- Colour is never the sole carrier of meaning: the active TOC item gets a left marker **and** weight,
  not just accent colour.

---

## 8. Exact files to create or change

| Path | Action | Purpose |
|---|---|---|
| `docs/_layouts/default.html` | **create** | The entire page chrome. Replaces Cayman's layout by shadowing it. |
| `docs/assets/css/site.css` | **create** | The entire stylesheet. Plain CSS, no front matter, served static. |
| `docs/assets/js/docs.js` | **create** | Copy buttons, sidebar TOC, heading anchors, scroll state. Deferred, optional. |
| `docs/assets/favicon.svg` | **create** | Copy of the existing `assets/logo.svg` (2,177 bytes, already in the repo root). |
| `docs/_config.yml` | **edit** | Add `author`/social metadata used by the hand-written meta tags. Theme key unchanged. |
| `docs/recipes.md` | **no change** | Deliberately. Not one byte. |
| `docs/index.md` | **no change required** | Optional: none of this spec needs it. |

Six files total for the docs site, up from three. No `_includes/` — **deliberately**: with one layout,
an include directory is indirection without reuse.

### 8.1 `docs/_config.yml` — diff

Only additions. `theme`, `permalink`, `exclude`, `title`, `description` stay exactly as they are.

```yaml
# (existing keys unchanged)

# Used by the hand-written meta tags in _layouts/default.html. No plugin reads these.
url: https://poindexter12.github.io
repo_url: https://github.com/poindexter12/jellyfin-plugin-smarterplaylist
readme_url: https://github.com/poindexter12/jellyfin-plugin-smarterplaylist#readme
```

**Do not add a `plugins:` key.** §8.6.

### 8.2 `docs/_layouts/default.html`

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <meta name="color-scheme" content="light dark">
  <title>{% if page.title and page.title != site.title %}{{ page.title }} · {{ site.title }}{% else %}{{ site.title }}{% endif %}</title>
  <meta name="description" content="{{ page.description | default: site.description }}">
  <link rel="canonical" href="{{ page.url | absolute_url }}">
  <link rel="icon" href="{{ '/assets/favicon.svg' | relative_url }}" type="image/svg+xml">
  <meta property="og:title" content="{{ page.title | default: site.title }}">
  <meta property="og:description" content="{{ page.description | default: site.description }}">
  <meta property="og:type" content="website">
  <meta property="og:url" content="{{ page.url | absolute_url }}">
  <meta name="twitter:card" content="summary">
  <link rel="stylesheet" href="{{ '/assets/css/site.css' | relative_url }}">
</head>
<body class="{% if page.title == 'Recipes' %}page-recipes{% else %}page-home{% endif %}">
  <a id="skip" href="#content">Skip to content</a>
  <span id="top"></span>

  <header class="site-header">
    <div class="bar">
      <a class="brand" href="{{ '/' | relative_url }}">{{ site.title }}</a>
      <nav aria-label="Site">
        <a href="{{ '/recipes/' | relative_url }}"
           {% if page.title == 'Recipes' %}aria-current="page"{% endif %}>Recipes</a>
        <a href="{{ site.readme_url }}" rel="noopener">Reference&nbsp;↗</a>
      </nav>
    </div>
  </header>

  <div class="shell">
    <aside class="toc-rail" hidden></aside>
    <main id="content" class="prose">
      <h1 class="page-title">{{ page.title }}</h1>
      {{ content }}
    </main>
  </div>

  <footer class="site-footer">
    <a href="{{ site.repo_url }}" rel="noopener">Smarter Playlist on GitHub</a>
  </footer>

  <a class="to-top" href="#top" aria-label="Back to top">↑</a>
  <script src="{{ '/assets/js/docs.js' | relative_url }}" defer></script>
</body>
</html>
```

Notes on decisions embedded above:

- `<h1 class="page-title">{{ page.title }}</h1>` is emitted by the **layout**, which is why neither
  Markdown file contains an `h1`. Recipes therefore remain `h2` and **all nine anchor ids are
  untouched** (§1, VERIFIED). Do not "promote" recipes to `h1`.
- `.toc-rail` ships `hidden` and empty. JS fills and unhides it; no-JS leaves it out of the
  accessibility tree entirely, with no empty landmark.
- Only core Liquid filters are used (`relative_url`, `absolute_url`, `default`). No plugin tags, so
  no plugin can fail to load. §8.6.
- `defer` on the script guarantees the JS never blocks first paint of the content.

### 8.3 `docs/assets/css/site.css`

Plain CSS. **No front matter** — it is a static asset, not a Jekyll-processed file.

```css
:root {
  --bg:#fbfcfd; --surface-code:#eef2f6; --border:#dbe3ea;
  --text:#1b2126; --text-muted:#59646e;
  --accent:#07698f; --accent-edge:#00a4dc;
  --tok-key:#0b5a8a; --tok-str:#9b2c2c; --tok-num:#7a4bbd; --tok-punc:#414b55;
  --font-sans:-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,"Helvetica Neue",Arial,sans-serif;
  --font-mono:ui-monospace,SFMono-Regular,"SF Mono",Menlo,Consolas,"Liberation Mono",monospace;
  --measure:68ch; --radius:8px;
}
@media (prefers-color-scheme: dark) {
  :root {
    --bg:#101418; --surface-code:#171d23; --border:#252d35;
    --text:#e6edf3; --text-muted:#93a1ad;
    --accent:#58c8f0; --accent-edge:#00a4dc;
    --tok-key:#7fd4f5; --tok-str:#f0a8a0; --tok-num:#c9aef5; --tok-punc:#9fb0bd;
  }
}

*,*::before,*::after { box-sizing:border-box; }
html { scroll-behavior:smooth; }
body {
  margin:0; background:var(--bg); color:var(--text);
  font:400 17px/1.65 var(--font-sans);
  -webkit-text-size-adjust:100%;
}
a { color:var(--accent); text-decoration:underline; text-underline-offset:2px; }
a:hover { text-decoration-thickness:2px; }
:focus-visible { outline:2px solid var(--accent); outline-offset:2px; border-radius:2px; }

#skip { position:absolute; left:-9999px; }
#skip:focus {
  position:fixed; top:8px; left:8px; z-index:20; padding:8px 12px;
  background:var(--bg); border:1px solid var(--accent); border-radius:var(--radius);
}

/* ---- header ---- */
.site-header {
  position:sticky; top:0; z-index:10;
  background:color-mix(in srgb, var(--bg) 88%, transparent);
  backdrop-filter:saturate(180%) blur(8px);
  border-bottom:1px solid var(--border);
}
.bar {
  max-width:76rem; margin:0 auto; height:56px; padding:0 24px;
  display:flex; align-items:center; justify-content:space-between; gap:16px;
}
.brand { font-weight:700; font-size:17px; text-decoration:none; color:var(--text); }
.site-header nav { display:flex; gap:24px; font-size:14px; }
.site-header nav a { text-decoration:none; color:var(--text-muted); }
.site-header nav a:hover { color:var(--text); }
.site-header nav a[aria-current="page"] {
  color:var(--text); font-weight:700;
  box-shadow:inset 0 -2px 0 var(--accent-edge);
}

/* ---- shell ---- */
.shell { max-width:76rem; margin:0 auto; padding:0 24px; display:block; }
.prose { max-width:var(--measure); padding:32px 0 64px; }
.page-title { font-size:28px; line-height:1.2; font-weight:700; margin:0 0 16px; }

.prose h2 {
  font-size:28px; line-height:1.25; font-weight:700; color:var(--text);
  margin:0 0 8px; scroll-margin-top:72px;
}
.prose h3 { font-size:20px; line-height:1.4; font-weight:700; margin:32px 0 8px; }
.prose p { margin:0 0 16px; }
.prose > p + .highlight, .prose > p + figure.code { margin-top:16px; }
.prose hr { border:0; border-top:1px solid var(--border); margin:48px 0; }
.prose hr + h2 { margin-top:0; }
.prose blockquote {
  margin:16px 0; padding:8px 16px; color:var(--text-muted);
  border-left:3px solid var(--accent-edge);
}
.prose ul, .prose ol { margin:0 0 16px; padding-left:24px; }
.prose li { margin-bottom:4px; }
.prose code {
  font:400 15px/1.4 var(--font-mono);
  background:var(--surface-code); border:1px solid var(--border);
  border-radius:4px; padding:1px 4px;
}

/* ---- chip row: the existing index list on recipes.md ---- */
.page-recipes .prose > ul:first-of-type {
  list-style:none; margin:24px 0 48px; padding:0;
  display:flex; flex-wrap:wrap; gap:8px;
}
.page-recipes .prose > ul:first-of-type li { margin:0; }
.page-recipes .prose > ul:first-of-type a {
  display:block; padding:8px 12px; font-size:14px; text-decoration:none;
  color:var(--text); background:var(--surface-code);
  border:1px solid var(--border); border-radius:999px;
}
.page-recipes .prose > ul:first-of-type a:hover { border-color:var(--accent-edge); }
@media (pointer: coarse) {
  .page-recipes .prose > ul:first-of-type a { padding:12px 16px; }
}

/* ---- code blocks ---- */
figure.code { position:relative; margin:16px 0 24px; }
.prose .highlight, figure.code .highlight {
  margin:0; background:var(--surface-code);
  border:1px solid var(--border); border-left:3px solid var(--accent-edge);
  border-radius:var(--radius); overflow:hidden;
}
.prose .highlight pre, .prose pre {
  margin:0; padding:16px; overflow-x:auto; -webkit-overflow-scrolling:touch;
  background:transparent; border:0;
  font:400 14px/1.55 var(--font-mono); color:var(--text); white-space:pre;
}
.prose .highlight pre code { background:none; border:0; padding:0; font-size:inherit; }
@media (min-width: 64em) {
  figure.code, .prose > .highlight { margin-left:-16px; margin-right:-16px; }
}

.highlight .nl { color:var(--tok-key); font-weight:700; }
.highlight .s2, .highlight .s, .highlight .s1 { color:var(--tok-str); }
.highlight .mi, .highlight .mf, .highlight .m { color:var(--tok-num); }
.highlight .kc { color:var(--tok-num); font-weight:700; }
.highlight .p, .highlight .o { color:var(--tok-punc); }
.highlight .w { color:inherit; }
.highlight .err { color:var(--text); background:rgba(220,60,60,.18); }

/* ---- copy button ---- */
figure.code .copy {
  position:absolute; top:8px; right:8px; z-index:2;
  height:32px; padding:0 12px;
  font:400 14px/1 var(--font-sans); color:var(--text-muted);
  background:var(--bg); border:1px solid var(--border);
  border-radius:var(--radius); cursor:pointer;
  opacity:0; transition:opacity .12s ease;
}
figure.code:hover .copy, figure.code .copy:focus-visible { opacity:1; }
figure.code .copy:hover { color:var(--text); border-color:var(--accent-edge); }
figure.code .copy[data-state="done"] { opacity:1; color:var(--accent); border-color:var(--accent); }
@media (pointer: coarse) { figure.code .copy { opacity:1; height:44px; } }

/* ---- sidebar TOC (JS only) ---- */
.toc-rail { display:none; }
@media (min-width: 64em) {
  .shell { display:grid; grid-template-columns:15rem minmax(0,1fr); gap:48px; }
  .html-has-toc .toc-rail { display:block; }
  .toc-rail nav { position:sticky; top:80px; padding:32px 0; font-size:14px; }
  .toc-rail ul { list-style:none; margin:0; padding:0; }
  .toc-rail li { margin:0; }
  .toc-rail a {
    display:block; padding:4px 12px; color:var(--text-muted); text-decoration:none;
    border-left:2px solid var(--border);
  }
  .toc-rail a:hover { color:var(--text); }
  .toc-rail a[aria-current="true"] {
    color:var(--text); font-weight:700; border-left-color:var(--accent-edge);
  }
  .html-has-toc.js .page-recipes .prose > ul:first-of-type { display:none; }
}

/* ---- heading anchors (JS only) ---- */
.prose h2 .anchor {
  margin-left:8px; text-decoration:none; color:var(--text-muted);
  opacity:0; font-weight:400;
}
.prose h2:hover .anchor, .prose h2 .anchor:focus-visible { opacity:1; }

/* ---- footer + back to top ---- */
.site-footer {
  max-width:76rem; margin:0 auto; padding:24px;
  border-top:1px solid var(--border); font-size:14px; color:var(--text-muted);
}
.to-top {
  position:fixed; right:16px; bottom:16px; z-index:10;
  width:44px; height:44px; display:flex; align-items:center; justify-content:center;
  text-decoration:none; color:var(--text-muted);
  background:var(--bg); border:1px solid var(--border); border-radius:50%;
}
.to-top:hover { color:var(--text); border-color:var(--accent-edge); }
html.js:not(.is-scrolled) .to-top { display:none; }

@media (prefers-reduced-motion: reduce) {
  html { scroll-behavior:auto; }
  *,*::before,*::after { transition:none !important; animation:none !important; }
}
```

**One flagged dependency:** `color-mix()` in `.site-header`. Baseline in all current evergreen
browsers; in anything older the whole `background` declaration is dropped and the header falls back to
transparent over `--bg`. If that bothers you, replace with `background: var(--bg);` and delete the
`backdrop-filter` line — the design does not depend on translucency.

### 8.4 `docs/assets/js/docs.js`

Vanilla, no dependencies, no CDN, `defer`-loaded. Everything here is additive.

```js
(function () {
  var root = document.documentElement;
  root.classList.add('js');

  // 1. Copy buttons ------------------------------------------------------
  document.querySelectorAll('main .highlight').forEach(function (hl) {
    var fig = document.createElement('figure');
    fig.className = 'code';
    hl.parentNode.insertBefore(fig, hl);
    fig.appendChild(hl);

    var pre = fig.querySelector('pre');
    if (!pre) return;
    var text = pre.textContent.replace(/\n$/, '');

    var label = 'Copy';
    try {
      var parsed = JSON.parse(text);
      if (parsed && typeof parsed === 'object' && !Array.isArray(parsed) && 'Name' in parsed) {
        label = 'Copy definition';
      }
    } catch (e) { /* fragment or non-JSON: keep the generic label */ }

    var btn = document.createElement('button');
    btn.type = 'button';
    btn.className = 'copy';
    btn.textContent = label;
    btn.setAttribute('aria-live', 'polite');

    btn.addEventListener('click', function () {
      var done = function () {
        btn.textContent = 'Copied';
        btn.dataset.state = 'done';
        setTimeout(function () {
          btn.textContent = label;
          delete btn.dataset.state;
        }, 1600);
      };
      var fail = function () {
        btn.textContent = 'Press ⌘C';
        var r = document.createRange();
        r.selectNodeContents(pre);
        var s = window.getSelection();
        s.removeAllRanges();
        s.addRange(r);
        setTimeout(function () { btn.textContent = label; }, 2400);
      };
      if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(text).then(done, fail);
      } else {
        var ta = document.createElement('textarea');
        ta.value = text;
        ta.setAttribute('readonly', '');
        ta.style.cssText = 'position:absolute;left:-9999px';
        document.body.appendChild(ta);
        ta.select();
        try { document.execCommand('copy') ? done() : fail(); } catch (e) { fail(); }
        document.body.removeChild(ta);
      }
    });

    fig.appendChild(btn);
  });

  // 2. Heading anchors ---------------------------------------------------
  var heads = Array.prototype.slice.call(document.querySelectorAll('main h2[id]'));
  heads.forEach(function (h) {
    var a = document.createElement('a');
    a.className = 'anchor';
    a.href = '#' + h.id;
    a.textContent = '#';
    a.setAttribute('aria-label', 'Link to ' + h.textContent);
    h.appendChild(a);
  });

  // 3. Sidebar TOC + scroll-spy -----------------------------------------
  var rail = document.querySelector('.toc-rail');
  if (rail && heads.length > 2) {
    var nav = document.createElement('nav');
    nav.setAttribute('aria-label', 'On this page');
    var ul = document.createElement('ul');
    var links = {};
    heads.forEach(function (h) {
      var li = document.createElement('li');
      var a = document.createElement('a');
      a.href = '#' + h.id;
      a.textContent = h.firstChild ? h.firstChild.textContent.trim() : h.id;
      li.appendChild(a);
      ul.appendChild(li);
      links[h.id] = a;
    });
    nav.appendChild(ul);
    rail.appendChild(nav);
    rail.hidden = false;
    root.classList.add('html-has-toc');

    if ('IntersectionObserver' in window) {
      var seen = {};
      var io = new IntersectionObserver(function (entries) {
        entries.forEach(function (e) { seen[e.target.id] = e.isIntersecting; });
        var active = null;
        heads.forEach(function (h) { if (!active && seen[h.id]) active = h.id; });
        heads.forEach(function (h) {
          if (h.id === active) links[h.id].setAttribute('aria-current', 'true');
          else links[h.id].removeAttribute('aria-current');
        });
      }, { rootMargin: '-72px 0px -70% 0px' });
      heads.forEach(function (h) { io.observe(h); });
    }
  }

  // 4. Back-to-top visibility -------------------------------------------
  var tick = false;
  window.addEventListener('scroll', function () {
    if (tick) return;
    tick = true;
    requestAnimationFrame(function () {
      root.classList.toggle('is-scrolled', window.scrollY > 600);
      tick = false;
    });
  }, { passive: true });
})();
```

**Note on `heads.length > 2`:** the home page has only two `h2`s and gets no rail. The Recipes page
has eleven and does. No configuration, no front-matter flag — it decides from the content.

### 8.5 `docs/assets/favicon.svg`

`cp assets/logo.svg docs/assets/favicon.svg`. It already exists at 2,177 bytes. Do **not** reference
`../assets/logo.svg` — files outside `docs/` are not published by a `/docs`-rooted Pages build.

### 8.6 On `plugins:` — deliberately empty

VERIFIED: the allowlist includes `jekyll-seo-tag 2.8.0`, `jekyll-sitemap 1.4.0`,
`jekyll-redirect-from 0.16.0`, `jekyll-feed 0.17.0` and 17 others. **NEEDS VERIFICATION: which of
these the Pages legacy build auto-enables versus which require an explicit `plugins:` entry.** I did
not confirm this, and the failure mode is asymmetric — a `{% seo %}` tag whose plugin is not loaded is
an "Unknown tag" **build failure**, taking the whole site down, not a degraded page.

So the spec sidesteps it: §8.2 hand-writes eight meta tags, which is less markup than the config entry
plus the tag would be, and cannot fail. A two-page site does not need a sitemap or an RSS feed.

If you later want `jekyll-seo-tag`, the safe order is: add `plugins: [jekyll-seo-tag]` to
`_config.yml` **and** the `{% seo %}` tag in the same commit, then confirm the Pages build went green
before removing the hand-written tags.

---

## 9. Deliberately rejected — the tempting-but-wrong options

**9.1 Adding Prism.js or highlight.js from a CDN.** The single most tempting move, and wrong three
times over. (a) **Redundant** — Rouge already highlights server-side; VERIFIED, 10 highlighted blocks
in the live HTML. A client highlighter would re-tokenise already-tokenised markup or fight it.
(b) **Regressive** — highlighting would become JS-dependent, so a no-JS reader would go from
highlighted code today to plain code. (c) **A live third-party dependency** — a CDN that can go down,
change, or be blocked, in a project whose stated priority is surviving neglect. The correct move is
~14 lines of Rouge token CSS (§8.3), which is what this spec does.

**9.2 Splitting the nine recipes into nine pages or a `_recipes` collection.** Genuinely tempting —
it is what a "real" docs site looks like. Rejected because it **breaks nine live deep links that
nothing on GitHub Pages can repair**, since fragments never reach the server (§3). It also degrades
the github.com file view and destroys whole-cookbook Ctrl-F, in exchange for solving a scale problem
that does not exist at nine items.

**9.3 A Jekyll data file (`_data/recipes.yml`) driving cards.** The clean-architecture answer: recipe
titles, blurbs and tags in structured data, rendered into a card grid by Liquid. Rejected because it
**duplicates every recipe title and intent line outside `recipes.md`**, creating a drift surface that
a solo maintainer will forget on the first edit. The chip row (§3.3) reads the same information from
the Markdown that is already there, and the sidebar TOC (§5.4) derives from the rendered headings.
Neither can drift, because neither is a copy.

**9.4 Kramdown IALs (`{: .recipe-card}`) or raw HTML wrappers in `recipes.md`.** The direct way to get
per-block classes. Both **disqualified by the hard constraint**: kramdown IALs render as literal
`{: .recipe-card}` text on github.com, and a `<div markdown="1">` wrapper renders its inner Markdown
literally under GitHub's CommonMark pipeline. Either way, visible junk in the repo file view. Every
structural hook in this spec is therefore a CSS selector over Markdown that was already there.

**9.5 A manual light/dark toggle.** Needs JS, a storage key, a render-blocking inline script to avoid
a flash of the wrong theme, plus a header control and its own a11y state. Four moving parts to
override a preference the reader already set at OS level. `prefers-color-scheme` is ~12 lines and
zero JS (§6.5). If a toggle is ever added, it must be **additive** to the media query, never a
replacement for it.

**9.6 Just the Docs / Minimal Mistakes via `remote_theme`.** Allowlisted and technically available;
rejected in §2 on build-time-network-fetch fragility, static-not-automatic dark mode, and a
maintenance surface wildly out of proportion to two pages.

**9.7 Switching to a different built-in theme.** Rejected in §2 on capability: **none of the thirteen
allowlisted themes has dark mode, navigation chrome, or a TOC**, so no swap can satisfy requirements
3 and 5.

**9.8 Client-side search over the docs.** Two pages. Ctrl-F already works and works better,
*because* §3 kept everything in one file.

**9.9 Rewriting or restructuring the recipe copy.** Out of scope by instruction, and unnecessary —
every hierarchy problem in §1 is solved by weight, spacing and rules over the existing text.

**9.10 Soft-wrapping long JSON lines to avoid horizontal scroll.** Tempting for mobile. Rejected: the
column alignment in the M\*A\*S\*H and Best-of-the-90s recipes is deliberate and load-bearing, and
wrapping mid-object makes a copy-paste payload harder to verify by eye, not easier.

---

## 10. Acceptance checklist

Verifiable by inspection after implementation.

- [ ] All nine `#anchor` deep links from the current site still resolve, and each lands with its
      heading fully visible below the sticky header (`scroll-margin-top`).
- [ ] `docs/recipes.md` is byte-identical to its pre-change state.
- [ ] Viewing `docs/recipes.md` on github.com shows no stray syntax — no IALs, no HTML wrappers.
- [ ] With JavaScript disabled: header, nav, chip row, hierarchy, code styling, syntax highlighting,
      dark mode and back-to-top all work. Only copy buttons, the sidebar rail and heading anchors are
      absent.
- [ ] `prefers-color-scheme: dark` produces a full dark theme with no flash of white on load.
- [ ] Every contrast pair in §6.3 measures at or above its stated ratio.
- [ ] Copy button on a recipe reads "Copy definition"; on the "Combining these" fragment and on the
      install-URL block it reads "Copy".
- [ ] Copied text is the JSON only — no button label, no trailing blank line.
- [ ] No request to `fonts.googleapis.com`, `fonts.gstatic.com`, or any CDN in the network panel.
- [ ] Cayman's `/assets/css/style.css` is not requested by any page.
- [ ] Full keyboard traverse of `/recipes/` reaches skip link → nav → chips → every copy button →
      back-to-top, with a visible focus ring at every stop.
- [ ] The Pages build goes green on the first push (no `plugins:` key was added, so no tag can fail
      to resolve).
