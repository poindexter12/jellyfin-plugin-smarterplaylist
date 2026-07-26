# External Integrations

**Analysis Date:** 2026-07-25

## APIs & External Services

**None.** The plugin makes no outbound network calls. Every dependency is either the Jellyfin host process or the local filesystem.

**Host platform (in-process, via constructor injection):**
- Jellyfin server 10.11 — the only integration surface
  - SDK/Client: `Jellyfin.Controller` / `Jellyfin.Model` 10.11.11
  - Auth: n/a — services are handed to the plugin by the server's DI container

Interfaces consumed, and where:
- `IApplicationPaths`, `IXmlSerializer` — `Plugin.cs` (plugin registration and configuration storage)
- `IServerApplicationPaths` — `SmarterPlaylistFileSystem.cs` (locating the data directory)
- `ILibraryManager` — `ScheduleTasks/RefreshAllPlaylists.cs`, `QueryEngine/OperandFactory.cs` (enumerating library items, resolving credited people)
- `IUserDataManager` — `QueryEngine/OperandFactory.cs` (per-user play state)
- `IUserManager` — `ScheduleTasks/RefreshAllPlaylists.cs` (resolving a playlist's owner by name)
- `IPlaylistManager` — `ScheduleTasks/RefreshAllPlaylists.cs` (creating and populating playlists)

## Data Storage

**Databases:**
- None owned by the plugin. Library and user data are read through Jellyfin's managers, which sit in front of the server's own database.

**File Storage:**
- Local filesystem only. `SmarterPlaylistFileSystem` creates and owns `<IServerApplicationPaths.DataPath>/SmarterPlaylists/`, holding one hand-authored `*.json` file per playlist definition.
- Read/write goes through `SmarterPlaylistStore` using `System.Text.Json`.
- Note: the `userId` parameters on `ISmarterPlaylistFileSystem.GetSmarterPlaylistFilePaths` and `GetSmarterPlaylistPath` are accepted but ignored, so definitions are **not** partitioned per user despite the API implying they are.

**Caching:**
- None.

## Authentication & Identity

**Auth Provider:**
- None of its own. The plugin has no HTTP surface, no controllers, and no configuration page (the stub `IHasWebPages` implementation was removed because it registered an empty page).
- Identity is resolved indirectly: each playlist definition names a user in its `User` field, and `IUserManager.GetUserByName` maps that to a Jellyfin user. A definition naming an unknown user is logged and skipped.

## Monitoring & Observability

**Error Tracking:**
- None.

**Logs:**
- `Microsoft.Extensions.Logging` via `ILogger<Plugin>` injected into `RefreshAllPlaylists`. Output lands in the Jellyfin server log.
- `SerilogAnalyzer` enforces structured message templates; `jellyfin.ruleset` escalates CA2254 (template must be a static expression) to an error.

## CI/CD & Deployment

**Hosting:**
- Distributed as a Jellyfin plugin. `.github/workflows/publish.yaml` deploys release artifacts over SSH to a plugin-repository host.

**CI Pipeline:**
- GitHub Actions, all delegating to `jellyfin/jellyfin-meta-plugins` reusable workflows at `@master`:
  - `build.yaml` — build on push/PR to `main` and `feature/*`
  - `test.yaml` — test on push/PR to `main` and `feature/*`
  - `publish.yaml` — on published release or manual dispatch; deploys and updates the plugin manifest
  - `scan-codeql.yaml` — CodeQL on push/PR plus a weekly cron (`24 2 * * 4`)
  - `changelog.yaml`, `sync-labels.yaml`, `command-dispatch.yaml`, `command-rebase.yaml` — repo automation
- All workflows declare `permissions: read-all`.
- The `@master` refs above are correct — they pin the upstream `jellyfin/jellyfin-meta-plugins` reusable workflows, whose default branch really is `master`. Do not "fix" them to `main`.

## Environment Configuration

**Required env vars:**
- Runtime: none.
- Local development: `DOTNET_ROLL_FORWARD=LatestMajor` (supplied by `mise.toml`) so the `net9.0` test host runs on the pinned .NET 10 runtime.

**Secrets location:**
- GitHub Actions repository secrets, consumed only by `publish.yaml`: `DEPLOY_HOST`, `DEPLOY_USER`, `DEPLOY_KEY`.
- No secrets are read at plugin runtime.

## Webhooks & Callbacks

**Incoming:**
- None. The plugin exposes no HTTP endpoints.

**Outgoing:**
- None.

**Scheduled work:**
- `RefreshAllPlaylists` implements `IScheduledTask` and `IConfigurableScheduledTask`; Jellyfin discovers it automatically. Default trigger is a 30-minute interval (`TaskTriggerInfoType.IntervalTrigger`), overridable from the server's scheduled-task UI.

---

*Integration audit: 2026-07-25*
