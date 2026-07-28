# Documentation UX findings — Smarter Playlist recipes site

**Basis for this analysis:** `docs/index.md`, `docs/recipes.md`, `README.md` (read directly from
source), and the rendered pages at the two live URLs (fetched to confirm no site-wide nav/search/
cross-linking exists beyond what's in the markdown — confirmed: there is none).

**Real user data available:** four verbatim fragments, provided by the requester, origin unknown to
me:
> "doesn't filter by last played date" / "I use smart playlists to build a night mash playlist" /
> "and I remove things I've watched recently" / "i'm mostly using it to watch seasons of shows that
> have crossovers"

That is the entire evidence base. Everything below that isn't traceable to one of those four
fragments or to the site's own text is marked as inference, not finding, and confidence is stated
per item. I have not invented additional users, quotes, or usage statistics.

---

## 1. The jobs readers arrive with

**Evidenced jobs (tied to an actual quote) — confidence: high, n=1 quote each, single source:**

- **"Strip a comfort rewatch down to what I haven't burned out on yet."** Three of the four quotes
  point at one job: a recurring show, watched on shuffle, with recently-watched episodes temporarily
  excluded so the same handful of episodes don't keep resurfacing. This is **directly and fully
  served** — it's recipe 2, "Night shuffle that skips what you just watched," built around
  `LastPlayedDate LessThan now-30d`. Worth noting: the recipe's own example show is M\*A\*S\*H, which
  reads like the maintainer built this recipe *from* this exact feedback. Good — this is the one
  recipe with a confirmed real reader behind it.

- **"Watch a multi-show universe held together by crossovers, in some sensible order."** The fourth
  quote — "seasons of shows that have crossovers" — is a distinct job from franchise-in-one-name-family
  viewing. This job is **not well served** by the site (see §5). It's the second-most evidenced job
  in the whole corpus and the weakest-covered.

**Unevidenced jobs (inferred from what the plugin can do, not from a quote) — confidence: low,
zero direct evidence either way:** unwatched-documentary tracking, a rewatch pile, kid-safety
filtering, decade/rating filtering, background genre radio, recently-added shelves, single-season
viewing. These make up 7 of the site's 9 recipes. They may well be real, common jobs — self-hosted
media nerds plausibly want most of these — but nothing in the given data confirms it, and I'd flag
that explicitly rather than assume it. **What I'd want to validate:** whether these seven were
written from any real request (a forum thread, a DM, an issue) or are the maintainer's own use
cases generalized. If the latter, that's not wrong, but it's worth knowing which recipes are
demand-led and which are maintainer-intuition-led when deciding what to invest further polish in.

**Jobs the site does not serve at all:**
- "Why is my playlist empty" — a job every reader eventually has (the README calls this "the single
  most common problem"), and the recipes page gives it no path in.
- "I don't want to write JSON, show me what to click" — the primary interface (dropdown builder) has
  no recipe written in its terms anywhere on the site.
- "Exclude a genre/person/rating from an otherwise-broad playlist" — a very natural smart-playlist
  want (iTunes/Plex users do this constantly), never demonstrated despite `NotEqual` and
  `NotMatchRegex` existing in the operator table.

## 2. Where the current structure fails, specifically

1. **The one recipe with two matching quotes behind it is buried second in an arbitrary list, and
   the one job with a third quote behind it isn't a recipe at all.** The list order (Star Trek
   franchise → night shuffle → documentaries → …) tracks no evidenced demand signal. A reader
   scanning nine similarly-weighted bullet points has no way to tell "this one is well-trodden" from
   "this one is a maintainer's pet example." *Hits:* anyone arriving with the actual most-common
   want, who has to read past an unrelated recipe first.

2. **"Playlist is empty" has no path from the Recipes page.** Four of nine Gotchas are instances of
   exactly the failure the README labels its single most common problem (case-sensitive exact match,
   exact `SeriesName`, unrated exclusion, `Contains` needing a whole element) — each recipe
   re-explains a fragment of it locally, but none link to the README's "Rules that quietly match
   nothing" section, which has the full explanation and the mitigation (use the builder's value
   dropdown). *Hits:* someone who followed a recipe, got zero results, and has no signposted next
   step short of reading the whole README cold.

3. **Every recipe is JSON; the primary interface is dropdowns — and the site's own intro text
   only offers the JSON/file-editing paths.** Recipes.md's lead-in says "Copy one, change the
   names, drop it in your `SmarterPlaylists` folder — or paste it into the Advanced (JSON) tab." It
   never says "or set it up rule-by-rule in the Rules tab," even though the README states that's how
   *most users* build playlists and that they "never have to touch JSON." *Hits:* the described
   persona precisely — technically capable, not a programmer, comfortable clicking dropdowns, not
   necessarily comfortable pasting curly-brace JSON into a text field they've never seen before. This
   is the largest structural issue on the site (expanded in §3).

4. **The README/site split is stated, not maintained.** Three of the site's nine recipes
   (Unwatched Documentaries, Star Trek, Best of the 90s) are reproduced near-verbatim in the
   README's own "Examples" section, and the M\*A\*S\*H recipe is reproduced again in full — JSON block
   and prose — inside the README's "Rules that move with time" section. That's 4 of 9 recipes with a
   duplicate living in the "reference" doc, which undercuts the stated division of labor ("This site
   is for worked examples. The README is the reference") and creates a two-places-to-edit hazard: a
   future correction to one of those recipes (a gotcha reworded, an operator fixed) has no guarantee
   of reaching its twin. *Hits:* nobody today, but it's a standing correctness risk, and it signals
   internally that the "examples live here, reference lives there" claim isn't actually true.

5. **No cross-linking runs the other direction either.** Recipes never link forward into the
   README's deeper explanation of the concept their own Gotcha is a special case of (e.g., the
   rewatch-pile recipe's implicit reliance on `IsPlayed` semantics never points at the README's fuller
   treatment; the kid-safe recipe's regex-anchoring gotcha never points at the operator-compatibility
   table). *Hits:* a reader who wants slightly more than the recipe gives — currently their only
   option is to reread the whole README, not jump to the relevant paragraph.

6. **Flat, untagged list at n=9 with no self-selection cue.** Nothing marks which recipes are for TV
   viewing versus music versus kid-safety versus library hygiene. Minor at the current size, but it
   compounds with #1 (no evidenced ordering) to make scanning purely title-dependent.

## 3. The JSON-versus-builder tension — take a position

This is a real mismatch, and it's the most consequential finding here. Position:

**JSON-first is fine as a *secondary* path. It should not be the *only* path shown.** The README is
explicit that the builder is how most people use this plugin, and that its whole value proposition
(dropdowns constrained to what the rule engine actually accepts, value autocomplete drawn from the
real library, inline validation) exists specifically so a non-programmer never has to think in JSON.
The Recipes page then hands that same non-programmer a page of nothing but JSON, with instructions
that only cover "drop the file on disk" or "paste it into the Advanced tab" — both of which assume a
reader who is already comfortable treating configuration as text. That reader may exist (self-hosted
admins often are JSON-adjacent from Docker Compose, etc. — see §7, this is worth checking rather than
assuming), but the persona as described — capable sysadmin, not necessarily a programmer, doing this
for fun — is exactly the reader most likely to be quietly put off by a wall of `{ "MemberName":
"SeriesName", "Operator": "Equal", "TargetValue": "M*A*S*H" }` even though the three concepts inside
it (property, operator, value) are ones they'd recognize instantly as three dropdowns.

**What a recipe for a builder-only reader should look like:** the property/operator/value triples
already exist in every recipe's JSON — this is a presentation problem, not a content problem. Each
recipe should carry a builder-oriented table alongside (or instead of, with JSON collapsed under an
"or paste this" disclosure) the JSON block:

| Property | Operator | Value |
|---|---|---|
| Series Name | Equal | M\*A\*S\*H |
| Last Played Date | Relative to now → 30 days ago | (picked, not typed) |

...plus a line for Order and MaxItems the same way the config page presents them. This is a direct,
mechanical transcription of data the recipes already contain — cheap to produce, and it makes the
Recipes page teach the interface the majority of readers are actually looking at.

## 4. Is the README/site split right?

**The idea is right; the execution isn't.** Reference material (the full property table, the
operator-compatibility matrix, the JSON schema, troubleshooting) is genuinely a different shape of
content than goal-oriented worked examples, and folding all of that into the Recipes page would bury
the goal-oriented content the persona is actually scanning for. Splitting them is the correct call.

But as shipped, the split is not two non-overlapping stores of content pointing at each other — it's
one store with partial duplication and almost no pointing. Concretely, I'd:

- **Cut the README's "Examples" section to zero, or to one minimal illustrative fragment**, and
  replace the rest with "see the [Recipes] page." Right now it silently reproduces a third of the
  site's content.
- **Shrink the M\*A\*S\*H reproduction inside "Rules that move with time"** to the single rule it's
  illustrating (`LastPlayedDate LessThan now-30d`), not the whole definition, and link to the full
  recipe for the rest. Reusing the same concrete example across both docs for teaching purposes is
  fine — reusing it as a verbatim duplicate block is what to fix.
- **Add the missing forward links**: every recipe Gotcha that's a special case of a named README
  concept ("Rules that quietly match nothing," "Rules that move with time," the operator table)
  should say so and link there, instead of re-deriving a fragment of the same explanation locally.
- **Add the missing backward link**: the README's troubleshooting entry "My playlist is empty" is the
  single most useful sentence on the whole site for a stuck reader, and the Recipes page currently
  gives it zero visibility until the very last line of the page.

## 5. Missing recipes

**High confidence this is a real gap (backed by the one ambiguous but real quote):** a recipe for
watching a **crossover universe of separately-named shows** — e.g. several series that don't share a
name prefix, interleaved by air date. The existing "Watch a franchise in broadcast order" recipe only
demonstrates the case where `MatchRegex "^Star Trek"` works because every series in the franchise
shares a literal name prefix. That technique does not generalize to shows that reference each other
without sharing a name (the mechanism the reader would actually need — one `ExpressionSet` per named
show, OR'd together, sorted `Release Date Ascending` — is already documented in the abstract under
"Combining these," but never assembled into a recipe someone with this exact want would recognize as
theirs). **Open question before writing it:** "shows that have crossovers" is genuinely ambiguous
between (a) several distinctly-named series in a shared universe, watched interleaved by air date, or
(b) something else entirely (e.g., wanting the crossover episodes specifically, which this plugin has
no way to identify at all — there's no crossover-tagging property). I would not guess; see §7.

**Lower confidence, capability gaps with no demand evidence either way — worth having only if
support requests confirm the want, not worth writing speculatively:**
- An **exclusion** recipe (`NotMatchRegex`/`NotEqual`) — e.g. "everything except a genre/rating" —
  the operator exists, is documented in the reference table, and is never demonstrated in a single
  recipe. All nine current recipes are purely inclusive.
- A **runtime-based** recipe (`RunTimeMinutes`) — e.g. "short films for a quick watch" — property
  exists in the reference table, never used in a recipe.
- A **cast-based** (not director-based) recipe using `Actors`/`GuestStars` — both properties exist,
  neither appears in any site recipe (only `Directors` appears, and only in the README's duplicated
  example, not on the site at all).

## 6. Prioritised recommendations

**P1 — Add builder-oriented steps to every recipe, JSON secondary.** *Impact: high — fixes the
single biggest mismatch between what the docs teach and what most readers actually use.* *Effort:
low — the property/operator/value data already exists in each JSON block; this is reformatting, not
new research or new copy.*

**P2 — Write the crossover-universe recipe, explicitly contrasted with the Star Trek prefix-regex
recipe (name the difference: shared-prefix vs. separately-named).** *Impact: high — this is the one
concrete content gap with direct evidence behind it (§5).* *Effort: low — reuses a pattern (multiple
OR'd single-rule groups + release-date sort) that's already fully documented in "Combining these";
no new mechanism to explain.* Confirm the "crossover" ambiguity first if there's any cheap way to
re-read the original feedback (see §7) — five minutes of clarity here changes the recipe's shape.

**P3 — Cross-link every Gotcha to its matching README section, and surface "playlist is empty" from
the Recipes page itself** (a one-line note near the top, not buried at the bottom). *Impact:
medium — closes the discoverability gap for the plugin's own stated most-common failure mode.*
*Effort: low — the target anchors already exist; this is adding links, not writing new content.*

**P4 — De-duplicate README/Recipes overlap:** trim the README's "Examples" section and the
M\*A\*S\*H reproduction down to fragments-plus-links. *Impact: medium — mostly a correctness/
maintenance-risk fix, secondarily a UX fix (stops two docs quietly disagreeing over time).*
*Effort: medium — requires editing already-published reference prose carefully so the README doesn't
lose its own illustrative value; more of a "sit down and do it properly" task than a quick patch.*

**P5 — Reorder recipes.md by evidenced demand and add light self-selection tags** (e.g. "for TV
watching," "for background listening," "for a shared/kid household"). Put the night-shuffle recipe
first, the new crossover recipe second. *Impact: medium — helps scanning, and stops the page implying
false equal-weighting across evidenced and unevidenced jobs.* *Effort: low.*

**P6 — Fix the Recipes intro line to mention the Rules-tab path, not just file-editing and the
Advanced/JSON tab.** *Impact: small on its own, but it's the sentence a first-time reader hits before
anything else, so it's disproportionately cheap leverage.* *Effort: trivial — one sentence.*

**What's already working — don't break it:**
- Every recipe is a complete, standalone definition rather than a fragment. Good discipline for this
  audience; keeps the JSON-paste path low-error even while it's being made secondary, not removed.
- The "Why it works" / "Gotcha" pairing is genuinely good pedagogy for this audience — it explains
  mechanism, not just outcome, and the Gotchas consistently target the exact failure modes the README
  independently flags as most common. Keep this shape for both the JSON and the new builder-oriented
  presentation.
- The homepage's "Two things that surprise people" callout is well-chosen and appropriately short — it
  pre-empts the two failure modes that recur across most of the recipes' Gotchas, before the reader
  even reaches Recipes. Don't dilute it by trying to make it exhaustive.

## 7. What I'd want to validate with real users, and how to get it cheaply

**Open questions, ranked by how much they'd change the docs:**

1. What did "seasons of shows that have crossovers" actually mean? This single ambiguous phrase
   drives the highest-priority content recommendation (P2), and I only have a paraphrase. **Cheapest
   fix:** go back to wherever these quotes came from (a Reddit thread, a Jellyfin forum post, a GitHub
   Discussion, a plugin-catalogue review) and read the full comment in context — that's a five-minute
   task for the maintainer and removes a real guess I've had to make here.

2. Do builder-only readers actually stall out on a JSON-only recipes page, or is the self-hosted
   Jellyfin admin audience more JSON-comfortable than the "not necessarily a programmer" framing
   assumes (plausible — people who run Docker Compose stacks touch JSON/YAML constantly even if they
   don't write code)? This is the load-bearing assumption behind P1 and I have zero direct evidence
   either way — it's inference from the stated persona plus the README's own claim about builder
   usage, not from a quote. **Cheapest fix:** an unmoderated 5-minute task with two or three known
   real users (Discord, a Jellyfin subreddit DM, anyone who's filed a plugin PR) — "here's the recipes
   page, build the night-shuffle playlist for a show you actually watch" — and just watch whether they
   open the Rules tab or the Advanced/JSON tab first. Two people settles this cheaply; it doesn't need
   a formal study.

3. Are the seven unevidenced recipes (documentaries, rewatch pile, kid-safe, decade, background
   genre, recently-added, one-season) actually requested by anyone, or generalized from the
   maintainer's own use? Not urgent to resolve, but worth knowing before investing more polish
   (builder tables, cross-links) in one over another. **Cheapest fix:** none needed proactively — just
   don't over-invest further effort ranking these against each other until a real signal shows up.

4. Is there any organic demand for exclusion-style or runtime-based recipes? No evidence currently
   exists. **Cheapest fix, ongoing and free:** enable a place for feedback to land — GitHub
   Discussions is free and already available on GitHub, or a one-line "missing your use case? open a
   discussion" prompt at the bottom of the Recipes page. This also captures future signal for #1 and
   #3 going forward, at effectively zero cost, without ever running a moderated study.

5. Lower-cost passive signal: a free, cookie-free analytics snippet (GoatCounter, Plausible free
   tier) on the GitHub Pages site would show which recipe anchors get scrolled to or referred into,
   giving a relative-demand ranking without asking anyone anything. This is a small config addition,
   not a design change — flagging it here rather than acting on it, since it sits right at the edge of
   this agent's remit and the parent should decide whether it's in scope.

None of the above requires a formal research budget — all four are things a solo maintainer can do
in spare cycles: reread one old thread, DM two or three known users for a five-minute unmoderated
task, and turn on a free feedback/analytics surface that keeps collecting signal for free afterward.
