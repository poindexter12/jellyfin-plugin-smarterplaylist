---
layout: default
title: Recipes
---

Complete playlist definitions for things people actually want. Copy one, change the names, drop it in
your `SmarterPlaylists` folder — or paste it into the **Advanced (JSON)** tab of the configuration
page.

Every recipe is a whole definition, not a fragment, so it works as-is once you change `User` to your
own account.

- [Watch a franchise in broadcast order](#watch-a-franchise-in-broadcast-order)
- [Night shuffle that skips what you just watched](#night-shuffle-that-skips-what-you-just-watched)
- [Unwatched documentaries, newest first](#unwatched-documentaries-newest-first)
- [Recently added to the library](#recently-added-to-the-library)
- [The rewatch pile](#the-rewatch-pile)
- [A kid-safe playlist](#a-kid-safe-playlist)
- [Best of a decade](#best-of-a-decade)
- [Background music by genre](#background-music-by-genre)
- [One show, one season](#one-show-one-season)

---

## Watch a franchise in broadcast order

Every Star Trek series interleaved the way it originally aired.

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

**Why it works.** `MatchRegex` on `SeriesName` catches every series whose name starts with the
franchise, so you do not have to list them. `Release Date Ascending` sorts by premiere date across all
of them, which is what produces the interleaving.

**Gotcha.** Star Trek across all series is close to 900 episodes and the default cap is 1000 — set
`MaxItems` deliberately for anything this size. Use **Preview matches** to see the real number before
saving. For one show at a time rather than interleaved, use `"Series, Season, Episode"`.

---

## Night shuffle that skips what you just watched

Every episode of a comfort show, minus anything played in the last month. Hit shuffle and it plays
from what is left; episodes rejoin the pool on their own as they age.

```json
{
  "Name": "M*A*S*H at night",
  "FileName": "mash_night",
  "User": "rob",
  "ExpressionSets": [
    {
      "Expressions": [
        { "MemberName": "SeriesName",     "Operator": "Equal",    "TargetValue": "M*A*S*H" },
        { "MemberName": "LastPlayedDate", "Operator": "LessThan", "TargetValue": "now-30d" }
      ]
    }
  ],
  "Order": { "Name": "NoOrder" }
}
```

**Why it works.** `now-30d` is re-evaluated on every refresh, so the window travels with you. Written
as a fixed date it would slowly stop excluding anything.

**Gotcha.** Episodes you have **never** watched are included too. They have no last-played date, and
an unset date is before every cutoff. That is normally what you want here — but if you only want
things you have seen before, add `{ "MemberName": "IsPlayed", "Operator": "Equal", "TargetValue": "True" }`.

Shorten or lengthen the memory by changing the offset: `now-7d` for a week, `now-3m` for a quarter.

---

## Unwatched documentaries, newest first

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

**Why it works.** The playlist empties itself as you watch: once an item is played it stops matching
and drops out at the next refresh.

**Gotcha.** `Contains` on a list property matches a **whole element exactly** and is case-sensitive —
`"Documentary"` will not match a genre stored as `"Documentaries"`. Pick the value from the dropdown
in the rule builder, which offers what your library actually contains.

---

## Recently added to the library

A shelf of what arrived in the last fortnight.

```json
{
  "Name": "Just Added",
  "FileName": "just_added",
  "User": "rob",
  "MaxItems": 40,
  "ExpressionSets": [
    { "Expressions": [ { "MemberName": "DateCreated", "Operator": "GreaterThan", "TargetValue": "now-14d" } ] }
  ],
  "Order": { "Name": "Release Date Descending" }
}
```

**Why it works.** `DateCreated` is when the item entered your library, not when it was released, so
this catches old films you just acquired.

**Gotcha.** A large import lands everything at once and will fill this playlist with a single batch.

---

## The rewatch pile

Things you have seen, but not for a long time.

```json
{
  "Name": "Time For A Rewatch",
  "FileName": "rewatch",
  "User": "rob",
  "MaxItems": 30,
  "ExpressionSets": [
    {
      "Expressions": [
        { "MemberName": "IsPlayed",       "Operator": "Equal",    "TargetValue": "True" },
        { "MemberName": "LastPlayedDate", "Operator": "LessThan", "TargetValue": "now-1y" }
      ]
    }
  ],
  "Order": { "Name": "NoOrder" }
}
```

**Why it works.** `IsPlayed` being `True` is what keeps never-watched items out — the exact opposite
of the night-shuffle recipe, where you wanted them in.

---

## A kid-safe playlist

```json
{
  "Name": "Kids",
  "FileName": "kids",
  "User": "rob",
  "MaxItems": 200,
  "ExpressionSets": [
    {
      "Expressions": [
        { "MemberName": "OfficialRating", "Operator": "MatchRegex", "TargetValue": "^(G|PG|TV-Y|TV-Y7|TV-G)$" }
      ]
    }
  ],
  "Order": { "Name": "NoOrder" }
}
```

**Why it works.** One regex with alternation is shorter than one rule group per rating. The anchors
matter: without `^` and `$`, `PG` would also match `PG-13`.

**Gotcha.** Items with no rating at all do not match, which is the safe default — but it also means
anything unrated is silently excluded even if it is fine. Check what your library actually uses; the
value dropdown lists the ratings present.

---

## Best of a decade

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

**Why it works.** Two rules on the same property give you a range, because rules within a group are
AND'd.

**Gotcha.** `CommunityRating` is the 0–10 user score. `CriticRating` is a 0–100 percentage — mixing
them up gives a playlist that matches everything or nothing.

---

## Background music by genre

```json
{
  "Name": "Jazz",
  "FileName": "jazz",
  "User": "rob",
  "MaxItems": 500,
  "ExpressionSets": [
    {
      "Expressions": [
        { "MemberName": "MediaType", "Operator": "Equal",    "TargetValue": "Audio" },
        { "MemberName": "Genres",    "Operator": "Contains", "TargetValue": "Jazz" }
      ]
    }
  ],
  "Order": { "Name": "NoOrder" }
}
```

**Gotcha.** Without the `MediaType` rule this would also pull in films tagged Jazz.

---

## One show, one season

```json
{
  "Name": "The Wire, Season 1",
  "FileName": "wire_s1",
  "User": "rob",
  "ExpressionSets": [
    {
      "Expressions": [
        { "MemberName": "SeriesName",   "Operator": "Equal", "TargetValue": "The Wire" },
        { "MemberName": "SeasonNumber", "Operator": "Equal", "TargetValue": "1" }
      ]
    }
  ],
  "Order": { "Name": "Series, Season, Episode" }
}
```

**Gotcha.** `SeriesName` and `SeasonNumber` are only set on episodes, so this never matches films.
`Equal` on `SeriesName` is exact and case-sensitive — `"the wire"` matches nothing.

---

## Combining these

Rules **within a group** are AND'd; **groups** are OR'd. To merge two recipes into one playlist, put
each one's rules in its own group:

```json
"ExpressionSets": [
  { "Expressions": [ { "MemberName": "Genres", "Operator": "Contains", "TargetValue": "Comedy" } ] },
  { "Expressions": [ { "MemberName": "Genres", "Operator": "Contains", "TargetValue": "Horror" } ] }
]
```

That is "comedy **or** horror". Putting both in a single group would ask for items that are somehow
both at once.

---

[Back to the start](../) · [Full reference in the README](https://github.com/poindexter12/jellyfin-plugin-smarterplaylist#readme)
