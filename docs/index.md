---
layout: default
title: Home
---

Playlists that build themselves from rules you write once.

This site is for **worked examples**. The [README][readme] is the reference: install, the rule
builder, every property and operator, the JSON format, troubleshooting.

## Install

Add this as a plugin repository in Jellyfin under *Dashboard → Plugins → Repositories*, then install
**Smarter Playlist** from the catalogue and restart:

```
https://raw.githubusercontent.com/poindexter12/jellyfin-plugin-smarterplaylist/main/manifest.json
```

## Start here

- **[Recipes](recipes/)** — complete playlist definitions for common goals. Copy one, change the
  `User`, done.

## Quick orientation

A rule is three things: a **property**, an **operator**, and a **value**.

> `Genres` `Contains` `Documentary`

Rules **within a group** are AND'd. **Groups** are OR'd. That is the whole model.

Playlists are rebuilt by a scheduled task every 30 minutes, and saving a definition builds it
immediately. Each rebuild replaces the contents from the definition — so anything you add to a
generated playlist by hand disappears, and **watched state is never touched**, because it belongs to
the library item rather than the playlist.

## Two things that surprise people

**Text matching is exact and case-sensitive.** On a list property, `Contains` wants a whole element:
`"Grey"` will not find a director called `CGP Grey`. Use `MatchRegex` for partial matches, or pick the
value from the dropdown in the rule builder, which offers what your library actually holds.

**Never-played items count as "played long ago".** They have no last-played date, so a rule like
`LastPlayedDate LessThan now-30d` selects both what you watched months back and everything you have
never seen. Usually what you want; add `IsPlayed Equal True` when it is not.

[readme]: https://github.com/poindexter12/jellyfin-plugin-smarterplaylist#readme
