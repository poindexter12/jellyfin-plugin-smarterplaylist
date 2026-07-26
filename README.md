# Smarter Playlist for Jellyfin

Playlists that build themselves from rules you write once — like smart playlists in iTunes or Plex.

Describe what you want ("unwatched documentaries directed by CGP Grey, newest first, capped at 100")
and the plugin keeps a real Jellyfin playlist in sync with it. Rules can match on genre, cast and
crew, studios, tags, ratings, dates, runtime, series, play state and more.

**Requires Jellyfin 10.11.** The plugin is built against the 10.11 server ABI and will not load on
older versions.

---

## Contents

- [Install](#install)
- [Make your first playlist](#make-your-first-playlist)
- [How refreshing works](#how-refreshing-works)
- [The configuration page](#the-configuration-page)
- [Writing rules](#writing-rules)
  - [What you can filter on](#what-you-can-filter-on)
  - [Which operators work on which properties](#which-operators-work-on-which-properties)
  - [Rules that quietly match nothing](#rules-that-quietly-match-nothing)
- [Combining rules with AND and OR](#combining-rules-with-and-and-or)
- [The JSON format](#the-json-format)
- [Examples](#examples)
- [Managing definitions](#managing-definitions)
- [Troubleshooting](#troubleshooting)
- [Future work](#future-work)
- [Releasing](#releasing)
- [Credits](#credits)

---

## Install

**From the plugin repository (recommended).** In Jellyfin, go to
*Dashboard → Plugins → Repositories → **+*** and add:

```
https://raw.githubusercontent.com/poindexter12/jellyfin-plugin-smarterplaylist/main/manifest.json
```

Then install **Smarter Playlist** from *Dashboard → Plugins → Catalogue* and restart Jellyfin.
Updates arrive through Jellyfin's normal plugin update flow.

**Manually.** Download the `.zip` from the
[releases page](https://github.com/poindexter12/jellyfin-plugin-smarterplaylist/releases), extract
`Jellyfin.Plugin.SmarterPlaylist.dll` into your Jellyfin plugin directory, and restart.

On first run the plugin creates a `SmarterPlaylists` folder in your Jellyfin data directory. Each
playlist is one `.json` file in there.

---

## Make your first playlist

1. Go to **Dashboard → Plugins → Smarter Playlist**.
2. Click **New definition** and give it a file name (letters, numbers, dots, dashes, underscores).
   This becomes the file on disk and cannot be changed later.
3. Set the **playlist name**, choose the **user** whose library it is built from, and pick a
   **sort order**.
4. Add rules. Pick a property, pick an operator, then pick or type a value. The dropdowns only offer
   combinations the rule engine accepts, so you cannot build a rule that fails at refresh time.
5. Click **Preview matches**. It reports how many items match, the first few titles in playlist
   order, and whether the maximum-items cap would discard any.
6. **Save.**

The playlist is created in Jellyfin as soon as you save, already filled with what the rules select.
It then stays in sync through the scheduled refresh below.

You never have to touch JSON — but the files are plain JSON and hand-editing them is fully
supported. See [The JSON format](#the-json-format).

---

## How refreshing works

A scheduled task named **"Refresh all SmarterPlaylists"** rebuilds every playlist from its
definition. It runs **every 30 minutes** by default.

To change the interval or run it now: *Dashboard → Scheduled Tasks → Refresh all SmarterPlaylists*.

Saving a definition builds its playlist there and then, so you do not have to wait for the task to
see it. The task is what keeps it current afterwards.

Each refresh re-evaluates the rules against the library and replaces the playlist's contents. So:

- New matching items appear automatically.
- Items that stop matching (you watched them, say) drop out.
- **Anything you add to the playlist by hand in Jellyfin is removed at the next refresh.** The
  definition is the source of truth.
- **Watched state is never affected.** It belongs to the library item, not to the playlist entry, so
  rebuilding a playlist leaves played flags and resume positions alone. A rule on `IsPlayed` is the
  one thing that connects them: watch something and it leaves an "unwatched" playlist next refresh.
- A playlist whose contents have not changed is left completely untouched, rather than rewritten.

One broken definition never stops the others. If a definition fails, the failure is recorded against
that definition and shown on the configuration page; every other playlist still refreshes.

---

## The configuration page

*Dashboard → Plugins → Smarter Playlist*

The page lists every definition with its target user, rule summary, item count, last refresh and
current status, so a playlist that is failing or matching nothing is visible without reading server
logs. Opening one gives you:

| | |
|---|---|
| **Rules tab** | The visual builder. Property, operator and value as dropdowns, with AND/OR groups. |
| **Advanced (JSON) tab** | The raw definition. Validated as you type. |
| **Preview matches** | What this definition selects right now, without saving. |
| **Save** | Validates first; refuses to write a definition the engine would reject. |
| **Delete** | Removes the definition, and asks what to do with the Jellyfin playlist. |

Everything the builder offers — the property list, the operators for each property, the value
controls — is derived from the rule engine itself, so the page cannot offer a rule the engine would
reject.

Hand-editing files on disk stays fully supported. If a file changes underneath an open editor, the
page tells you instead of overwriting the change.

---

## Writing rules

A rule is three things: **a property**, **an operator**, and **a value**.

> `Genres` `Contains` `Documentary`

### What you can filter on

| Group | Properties |
|---|---|
| **Title and location** | `Name`, `FolderPath` |
| **Series** | `SeriesName`, `SeasonName`, `SeasonNumber`, `EpisodeNumber` |
| **Music** | `Album` |
| **People** | `Actors`, `Directors`, `Writers`, `Producers`, `Composers`, `GuestStars` |
| **Classification** | `Genres`, `Studios`, `Tags`, `OfficialRating`, `MediaType` |
| **Ratings** | `CommunityRating` (0–10), `CriticRating` (0–100) |
| **Dates** | `PremiereDate`, `DateCreated`, `DateLastRefreshed`, `DateLastSaved`, `DateModified` |
| **Other** | `ProductionYear`, `RunTimeMinutes`, `IsPlayed` |

`SeriesName`, `SeasonName`, `SeasonNumber` and `EpisodeNumber` are only set on episodes. `Album` is
only set on audio.

### Which operators work on which properties

Which operators are valid depends on the property's type.

| Property type | Properties | Valid operators |
|---|---|---|
| Text | `Name`, `SeriesName`, `SeasonName`, `MediaType`, `Album`, `FolderPath`, `OfficialRating` | `Equal`, `NotEqual`, `Equals`, `Contains`, `StartsWith`, `EndsWith`, `MatchRegex`, `NotMatchRegex` |
| List of text | `Actors`, `Composers`, `Directors`, `Genres`, `GuestStars`, `Producers`, `Studios`, `Tags`, `Writers` | `Contains`, `MatchRegex`, `NotMatchRegex` |
| Number | `CommunityRating`, `CriticRating`, `SeasonNumber`, `EpisodeNumber`, `ProductionYear`, `RunTimeMinutes` | `Equal`, `NotEqual`, `GreaterThan`, `GreaterThanOrEqual`, `LessThan`, `LessThanOrEqual` |
| Date | `PremiereDate`, `DateCreated`, `DateLastRefreshed`, `DateLastSaved`, `DateModified` | `Equal`, `NotEqual`, `GreaterThan`, `GreaterThanOrEqual`, `LessThan`, `LessThanOrEqual` |
| True/false | `IsPlayed` | `Equal`, `NotEqual` |

Notes:

- **Text comparisons are case-sensitive.** `"comedy"` will not match `Comedy`.
- On a **list** property, `Contains` matches a **whole element exactly**. `"Contains": "Grey"` does
  *not* match a director named `CGP Grey` — it is not a substring search. Use `MatchRegex` for that.
- On a **list** property, `MatchRegex` matches if **any** element matches, and `NotMatchRegex` holds
  only if **no** element matches.
- **Dates** accept a readable date (`"2020-07-01"`, `"2020-07-01T00:00:00Z"`) or a raw Unix
  timestamp. Readable dates are treated as UTC.
- A **bare year** such as `"2020"` is rejected: it is indistinguishable from a Unix timestamp and
  would silently mean 1970. Write `"2020-01-01"`.
- `Equal` and `NotEqual` come from the
  [LINQ expression operators](https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions.expressiontype),
  so any name from that list is accepted — but only the ones above make sense per type.
- Using an operator a property does not support fails that playlist's refresh and logs the error.

### Rules that quietly match nothing

This is the single most common problem, and it is easy to hit: because comparisons are exact and
case-sensitive, a near-miss value saves cleanly, refreshes without error, and produces an empty
playlist. Nothing anywhere says you typed the name slightly wrong.

The configuration page prevents it. For the properties whose values your library already knows —
`Actors`, `Album`, `Composers`, `Directors`, `Genres`, `GuestStars`, `OfficialRating`, `SeasonName`,
`SeriesName`, `Studios`, `Tags`, `Writers` — the value field offers the values that are **actually
there**. Start typing and the list narrows.

- Values come from the library of **the user the definition names**, so choose the user first.
- You can still type something not offered — a value may name something not in the library yet.
- If what you typed is not a value the library holds, the page **says so under the field** instead of
  letting it save quietly and fail later.
- Values are not offered for `MatchRegex` / `NotMatchRegex`, where the value is a pattern, not a name.

On a very large library the list is capped; when it is, the page says so rather than implying a
missing value does not exist.

---

## Combining rules with AND and OR

Rules live in **groups**.

- Rules **within** a group are **AND**ed — all must match.
- **Groups** are **OR**ed — an item matches if it satisfies *any* group.

So this:

> **Group 1:** `Directors` contains `CGP Grey` **AND** `IsPlayed` is `False`
> **OR**
> **Group 2:** `Directors` contains `Nerdwriter1` **AND** `IsPlayed` is `False`

means "anything unwatched by either of these two directors". In the builder, **+ Add rule** adds to
the current group and **+ Add another rule group** starts a new OR branch. In JSON, a group is one
entry in `ExpressionSets`.

---

## The JSON format

Definitions live in the `SmarterPlaylists` folder in your Jellyfin data directory, one `.json` file
per playlist. This is the same file the configuration page reads and writes.

```json
{
  "Name": "CGP Grey",
  "FileName": "cgp_grey",
  "User": "rob",
  "MaxItems": 100,
  "ExpressionSets": [
    {
      "Expressions": [
        { "MemberName": "Directors", "Operator": "Contains", "TargetValue": "CGP Grey" },
        { "MemberName": "IsPlayed",  "Operator": "Equal",    "TargetValue": "False" }
      ]
    },
    {
      "Expressions": [
        { "MemberName": "Directors", "Operator": "Contains", "TargetValue": "Nerdwriter1" },
        { "MemberName": "IsPlayed",  "Operator": "Equal",    "TargetValue": "False" }
      ]
    }
  ],
  "Order": { "Name": "Release Date Ascending" }
}
```

| Field | Required | Meaning |
|---|---|---|
| `Name` | **yes** | The playlist's name as it appears in Jellyfin. |
| `User` | **yes** | Name of the user whose library the playlist is built from, and whose play state `IsPlayed` reflects. |
| `ExpressionSets` | **yes** | The rule groups — at least one, each with at least one rule. See [above](#combining-rules-with-and-and-or). |
| `FileName` | no | The definition's own file name, without `.json`. The name on disk always wins: if this field disagrees or is missing, the plugin takes it from the file rather than creating a second playlist. |
| `Order` | no | `{ "Name": "..." }` — see the sort orders below. Defaults to `NoOrder`. An unrecognised name is a warning, not an error, and falls back to `NoOrder`. |
| `MaxItems` | no | Caps how many items the playlist holds, applied **after** sorting, so you keep the first N in your chosen order. Omit or set `0` for the default of **1000**. |
| `Id` | never | Written by the plugin after the playlist is first created. Do not set it yourself. |

Each expression is `MemberName` (the property), `Operator`, and `TargetValue`. **`TargetValue` is
always a JSON string**, even for numbers, booleans and dates — it is converted to the property's
type when the rule is compiled.

Sort orders for `Order.Name`:

| Value | Effect |
|---|---|
| `NoOrder` | Whatever order the library returns. |
| `Release Date Ascending` | Oldest first, by premiere date. |
| `Release Date Descending` | Newest first, by premiere date. |
| `Series, Season, Episode` | Grouped by show, then in broadcast order within it. |

---

## Examples

### Everything unwatched in a genre, newest first

```json
{
  "Name": "Unwatched Documentaries",
  "FileName": "unwatched_docs",
  "User": "rob",
  "MaxItems": 50,
  "ExpressionSets": [
    {
      "Expressions": [
        { "MemberName": "Genres",   "Operator": "Contains", "TargetValue": "Documentary" },
        { "MemberName": "IsPlayed", "Operator": "Equal",    "TargetValue": "False" }
      ]
    }
  ],
  "Order": { "Name": "Release Date Descending" }
}
```

### A whole franchise, in order

```json
{
  "Name": "All of Star Trek",
  "FileName": "star_trek",
  "User": "rob",
  "MaxItems": 1000,
  "ExpressionSets": [
    { "Expressions": [ { "MemberName": "SeriesName", "Operator": "MatchRegex", "TargetValue": "^Star Trek" } ] }
  ],
  "Order": { "Name": "Release Date Ascending" }
}
```

`Release Date Ascending` plays the franchise as it was broadcast, so series interleave the way they
originally aired. `Series, Season, Episode` plays one show through to the end before starting the
next.

Set `MaxItems` deliberately for a franchise this size — the default cap is 1000 and Star Trek across
all series is close to 900 episodes. **Preview matches** tells you the real number before you save.

### Well-rated films from a decade

```json
{
  "Name": "Best of the 90s",
  "FileName": "best_90s",
  "User": "rob",
  "ExpressionSets": [
    {
      "Expressions": [
        { "MemberName": "MediaType",       "Operator": "Equal",              "TargetValue": "Video" },
        { "MemberName": "ProductionYear",  "Operator": "GreaterThanOrEqual", "TargetValue": "1990" },
        { "MemberName": "ProductionYear",  "Operator": "LessThanOrEqual",    "TargetValue": "1999" },
        { "MemberName": "CommunityRating", "Operator": "GreaterThanOrEqual", "TargetValue": "7.5" }
      ]
    }
  ],
  "Order": { "Name": "Release Date Ascending" }
}
```

---

## Managing definitions

### Deleting

Open the definition in the configuration page and use **Delete**. It asks what should happen to the
Jellyfin playlist the definition built:

- **Keep the Jellyfin playlist as a static list** *(default)*. The `.json` file goes; the playlist
  stays exactly as it was at the last refresh, and nothing updates it again. Choose this if anyone
  might have the playlist queued, favourited or shared.
- **Also delete the Jellyfin playlist.** Both go.

Deleting the file by hand from the definitions folder does the same as the first option.

### Renaming

There is no rename. A definition's file name is its identity, and the plugin refuses a save whose
`FileName` disagrees with the name on disk rather than silently writing a second file.

To rename: create a new definition under the name you want, then delete the old one. The new playlist
is built from scratch at the next refresh.

---

## Troubleshooting

**My playlist is empty.**
Almost always a value that does not match exactly — comparisons are case-sensitive, and `Contains`
on a list property needs a whole element, not a substring. Open the definition and use **Preview
matches**; if it reports 0, the rules are the problem, not the refresh. See
[Rules that quietly match nothing](#rules-that-quietly-match-nothing).

**My playlist has not appeared.**
Saving should create it immediately. If it did not, the page says why underneath the editor — most
often the definition names a user who does not exist. Fix that and save again, or run
*Dashboard → Scheduled Tasks → Refresh all SmarterPlaylists*.

**The configuration page shows a problem against my definition.**
The message names the property and what is wrong with it. Fix it in the editor and save — the save
is refused until the definition is valid, so it cannot fail at refresh time for the same reason.

**Items I added by hand disappeared.**
Expected. Each refresh replaces the playlist's contents from the definition.

**My playlist has fewer items than Preview said.**
`MaxItems` caps it after sorting. Preview reports when the cap would discard matches; the default is
1000.

**The plugin does not load at all.**
Check the server version. This build requires Jellyfin 10.11 and will not load on older servers.

---

## Future work

- More properties to match on. Please file a feature request if you have ideas — production year,
  official rating, tags, runtime and series name have all landed; what is left is whatever Jellyfin
  exposes that nobody has asked for yet.
- More sorting methods, such as by name, date added, or rating.
- Custom property types with custom operators. This is the big one — it would mean replacing the
  reflection-based rule engine with an explicit operator registry.
- Per-user self-service. Definitions are managed by an administrator naming a target user, because a
  plugin configuration page is only reachable from the dashboard. Letting users manage their own
  playlists needs a surface that is not the config page.

---

## Releasing

Releases are automated. Version numbers come from the labels on merged PRs, so labelling is the only
manual step.

1. Merge a PR into `main`. The **Create/Update Release Draft & Release Bump PR** workflow updates a
   draft release and opens a `Prepare for release vX.Y.Z` PR.
2. That PR bumps `version` in `build.yaml`, the three version elements in `Directory.Build.props`,
   and sets `targetAbi` from the referenced Jellyfin.Controller version. It also builds, so a version
   that would not compile never reaches a release.
3. Merge the prepare PR, then publish the draft release. Publishing triggers the **Publish Plugin**
   workflow, which attaches the artifact and regenerates `manifest.json` on `main`.

Version resolution follows the PR labels: `breaking`, `removed` or `major-feature` bump major;
`feature`, `major-bug` or `deprecated` bump minor; anything else is a patch. Label a PR
`skip-changelog` to leave it out of the notes. The label set is kept in sync from Jellyfin's shared
definitions by the **Sync labels** workflow.

Do not hand-edit versions. `Directory.Build.props` is the single source of truth for the assembly
version, and Jellyfin shows that version in the dashboard, so it and `build.yaml` are bumped together
to avoid showing two different numbers for one release.

---

## Credits

Rule engine was inspired by [this](https://stackoverflow.com/questions/6488034/how-to-implement-a-rule-engine)
post on Stack Overflow.

Initially wanted to convert [ppankiewicz's plugin](https://github.com/ppankiewicz/Emby.SmarterPlaylist.Plugin)
but found it too incompatible and difficult to work with. Some code was taken from it, mostly around
interfacing with the filesystem.
