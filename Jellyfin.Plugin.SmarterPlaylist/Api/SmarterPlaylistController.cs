using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.SmarterPlaylist.QueryEngine;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Playlists;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SmarterPlaylist.Api
{
    /// <summary>
    /// API backing the plugin's configuration page.
    /// </summary>
    [ApiController]
    [Authorize(Policy = Policies.RequiresElevation)]
    [Route("SmarterPlaylist")]
    [Produces(MediaTypeNames.Application.Json)]
    public class SmarterPlaylistController : ControllerBase
    {
        /// <summary>
        /// How many titles a preview returns. Small on purpose: it runs on every preview.
        /// </summary>
        private const int SampleSize = 10;

        /// <summary>
        /// Hard ceiling on how many values one field-values request returns, whatever it asks for.
        /// </summary>
        /// <remarks>
        /// A large library has tens of thousands of credited people. The response is rendered into a
        /// picker the browser holds in memory, so the cap is what keeps a cast-heavy library from
        /// turning a helpful list into an unusable one.
        /// </remarks>
        private const int MaxFieldValues = 5000;

        private static readonly BaseItemKind[] _supportedItems =
            [BaseItemKind.Audio, BaseItemKind.Episode, BaseItemKind.Movie];

        private static readonly JsonSerializerOptions _prettyOptions = new() { WriteIndented = true };

        private static readonly System.Text.RegularExpressions.Regex _safeFileName =
            new("^[A-Za-z0-9._-]{1,64}$", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1));

        private readonly ISmarterPlaylistFileSystem _fileSystem;
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger<Plugin> _logger;
        private readonly IPlaylistManager _playlistManager;
        private readonly IRefreshStatusStore _statusStore;
        private readonly ISmarterPlaylistStore _store;
        private readonly IUserDataManager _userDataManager;
        private readonly IUserManager _userManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="SmarterPlaylistController"/> class.
        /// </summary>
        /// <param name="fileSystem">Locates definition files.</param>
        /// <param name="libraryManager">Enumerates candidate items for preview, and removes deleted playlists.</param>
        /// <param name="logger">Logger for read failures.</param>
        /// <param name="playlistManager">Resolves the live playlist behind each definition.</param>
        /// <param name="statusStore">Last-refresh outcomes.</param>
        /// <param name="store">Removes definition files.</param>
        /// <param name="userDataManager">Resolves play state when projecting items for preview.</param>
        /// <param name="userManager">Resolves the user each definition names.</param>
        public SmarterPlaylistController(
            ISmarterPlaylistFileSystem fileSystem,
            ILibraryManager libraryManager,
            ILogger<Plugin> logger,
            IPlaylistManager playlistManager,
            IRefreshStatusStore statusStore,
            ISmarterPlaylistStore store,
            IUserDataManager userDataManager,
            IUserManager userManager)
        {
            _fileSystem = fileSystem;
            _libraryManager = libraryManager;
            _logger = logger;
            _playlistManager = playlistManager;
            _statusStore = statusStore;
            _store = store;
            _userDataManager = userDataManager;
            _userManager = userManager;
        }

        /// <summary>
        /// Gets the filter vocabulary, derived from the engine by reflection.
        /// </summary>
        /// <response code="200">Schema returned.</response>
        /// <returns>Every filterable member, valid orders and media types.</returns>
        [HttpGet("Schema")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<SchemaResponse> GetSchema() => SchemaBuilder.Build();

        /// <summary>
        /// Lists every playlist definition on disk, validated.
        /// </summary>
        /// <response code="200">Definitions returned.</response>
        /// <returns>The definitions, plus any files that could not be read.</returns>
        [HttpGet("Definitions")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<DefinitionsResponse>> GetDefinitions()
        {
            var schema = SchemaBuilder.Build();
            var statuses = _statusStore.GetAll();
            var summaries = new List<DefinitionSummary>();
            var loadErrors = new List<Diagnostic>();

            foreach (var path in _fileSystem.GetAllSmarterPlaylistFilePaths().OrderBy(p => p, StringComparer.Ordinal))
            {
                var fileName = Path.GetFileNameWithoutExtension(path);

                try
                {
                    var dto = await ReadDefinitionAsync(path).ConfigureAwait(false);
                    summaries.Add(BuildSummary(dto, schema, statuses));
                }
                catch (Exception ex) when (ex is JsonException or InvalidOperationException or IOException)
                {
                    // A file we cannot parse still has to appear, or the page would silently show
                    // fewer playlists than exist and the user would not know which one is broken.
                    _logger.LogError(ex, "Could not read playlist definition {File}", path);
                    loadErrors.Add(new Diagnostic("E00", DiagnosticSeverity.Error, $"{fileName}.json could not be read: {ex.Message}", fileName));
                }
            }

            return new DefinitionsResponse(_fileSystem.BasePath, summaries, loadErrors);
        }

        /// <summary>
        /// Gets a single definition with its raw JSON and validation results.
        /// </summary>
        /// <param name="fileName">On-disk name of the definition, without extension.</param>
        /// <response code="200">Definition returned.</response>
        /// <response code="404">No definition with that name exists.</response>
        /// <returns>The definition detail.</returns>
        [HttpGet("Definitions/{fileName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<DefinitionDetail>> GetDefinition([FromRoute] string fileName)
        {
            // Resolve against the files we already enumerate rather than building a path from the
            // route value. The request never reaches the file system as a path, so traversal is not
            // merely rejected -- it is unrepresentable, and the taint analyzer can see that.
            var path = _fileSystem.GetAllSmarterPlaylistFilePaths()
                .FirstOrDefault(p => string.Equals(Path.GetFileNameWithoutExtension(p), fileName, StringComparison.Ordinal));

            if (path is null)
            {
                return NotFound();
            }

            var raw = await System.IO.File.ReadAllTextAsync(path).ConfigureAwait(false);
            var schema = SchemaBuilder.Build();

            SmarterPlaylistDto dto;
            try
            {
                dto = JsonSerializer.Deserialize<SmarterPlaylistDto>(raw)
                    ?? throw new InvalidOperationException("The file is empty.");
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                var broken = new DefinitionSummary(
                    FileName: fileName,
                    Name: fileName,
                    User: string.Empty,
                    UserExists: false,
                    RuleSummary: "unreadable",
                    MaxItems: 0,
                    Order: string.Empty,
                    PlaylistState: PlaylistState.NotCreated,
                    PlaylistItemCount: null,
                    LastRefresh: _statusStore.Get(fileName),
                    Diagnostics: []);

                return Ok(new DefinitionDetail(
                    broken,
                    raw,
                    Hash(raw),
                    [new Diagnostic("E00", DiagnosticSeverity.Error, $"This file is not valid JSON: {ex.Message}", null)]));
            }

            dto.FileName = fileName;
            var summary = BuildSummary(dto, schema, _statusStore.GetAll());

            return new DefinitionDetail(summary, Pretty(raw), Hash(raw), summary.Diagnostics);
        }

        /// <summary>
        /// Reports what a definition would select right now, without saving or touching a playlist.
        /// </summary>
        /// <remarks>
        /// Evaluates the rules against the named user's library exactly as the scheduled task would,
        /// so the count shown is the count that would be applied. This is the fastest way to catch a
        /// rule that matches far more or far less than intended, which previously could only be
        /// discovered by waiting for the next refresh and inspecting the playlist.
        /// </remarks>
        /// <param name="request">The definition to evaluate.</param>
        /// <response code="200">Preview computed.</response>
        /// <response code="400">The definition is invalid, so it cannot be evaluated.</response>
        /// <returns>Match counts, a sample of titles, and what the scan cost.</returns>
        [HttpPost("Preview")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<PreviewResponse> Preview([FromBody] PreviewRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var inspection = Inspect(request.RawJson);
            if (inspection.Dto is null || inspection.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
            {
                return BadRequest(new ValidationProblem(inspection.Diagnostics));
            }

            var user = _userManager.GetUserByName(inspection.Dto.User);
            if (user is null)
            {
                return BadRequest(new ValidationProblem(
                [
                    new Diagnostic("E17", DiagnosticSeverity.Error, $"No user named '{inspection.Dto.User}' exists on this server.", "User")
                ]));
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var query = new InternalItemsQuery(user) { IncludeItemTypes = _supportedItems, Recursive = true };
            var items = _libraryManager.GetItemsResult(query).Items;

            // Flatten first, reading only what this definition's rules ask for. A preview of a rule
            // about genres does no per-item credit or play-state lookup at all, which is the whole
            // difference between a preview that returns promptly and one that walks the library twice.
            var playlist = new SmarterPlaylist(inspection.Dto);
            var candidates = OperandFactory.Project(
                _libraryManager, _userDataManager, items, user, playlist.ReferencedMembers);

            var filtered = playlist.FilterPlaylistItems(candidates);

            // Resolve a handful of titles in playlist order so the rules can be sanity-checked, not
            // just counted. Kept small deliberately: this runs on every preview.
            var sample = filtered.Ids
                .Take(SampleSize)
                .Select(id => _libraryManager.GetItemById(id)?.Name ?? id.ToString())
                .ToList();

            stopwatch.Stop();

            return new PreviewResponse(
                filtered.MatchedCount,
                filtered.Ids.Count,
                filtered.Truncated,
                sample,
                candidates.Count,
                stopwatch.ElapsedMilliseconds);
        }

        /// <summary>
        /// Lists the values a member actually takes in a user's library.
        /// </summary>
        /// <remarks>
        /// Backs the value pickers in the rule builder. Typing these by hand is the plugin's most
        /// common way to build a rule that quietly matches nothing — <c>Contains</c> compares a whole
        /// element exactly and case-sensitively, so <c>"Grey"</c> never finds <c>"CGP Grey"</c>, and
        /// the mistake surfaces only as an empty playlist after the next refresh. Offering the real
        /// values removes the mistake rather than documenting it.
        /// </remarks>
        /// <param name="member">Member to list values for.</param>
        /// <param name="user">Name of the user whose library to read.</param>
        /// <param name="limit">Most values to return. Clamped to 1..5000.</param>
        /// <response code="200">Values returned.</response>
        /// <response code="400">The member has no listable values, or the user does not exist.</response>
        /// <returns>The distinct values, sorted.</returns>
        [HttpGet("FieldValues")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<FieldValuesResponse> GetFieldValues(
            [FromQuery] string member,
            [FromQuery] string user,
            [FromQuery] int limit = 1000)
        {
            if (string.IsNullOrWhiteSpace(member) || !LibraryValueSource.IsSupported(member))
            {
                return BadRequest(new ValidationProblem(
                [
                    new Diagnostic(
                        "E18",
                        DiagnosticSeverity.Error,
                        $"There is no list of values for '{member}'. Type the value you want instead.",
                        "MemberName")
                ]));
            }

            var owner = string.IsNullOrWhiteSpace(user) ? null : _userManager.GetUserByName(user);
            if (owner is null)
            {
                return BadRequest(new ValidationProblem(
                [
                    new Diagnostic(
                        "E17",
                        DiagnosticSeverity.Error,
                        $"No user named '{user}' exists on this server, so their library cannot be read.",
                        "User")
                ]));
            }

            var cap = Math.Clamp(limit, 1, MaxFieldValues);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var kind = LibraryValueSource.PersonKindFor(member);
            var values = kind is null
                ? ItemBackedValues(member, owner)
                // PersonTypes is settable only through the constructor, so the query is built rather
                // than initialised.
                : _libraryManager.GetPeopleNames(new InternalPeopleQuery([kind.Value.ToString()], [])
                {
                    User = owner
                });

            // Ordinal, to match how the engine compares. A culture-aware sort would group "Émile"
            // next to "Emile" in the list and then fail to match either against the other.
            var sorted = values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList();

            stopwatch.Stop();

            return new FieldValuesResponse(
                member,
                sorted.Count > cap ? sorted.GetRange(0, cap) : sorted,
                sorted.Count > cap,
                stopwatch.ElapsedMilliseconds);
        }

        /// <summary>
        /// Collects a member's values by reading them off the items the engine would evaluate.
        /// </summary>
        /// <remarks>
        /// One query, then plain property reads. Deliberately not a full operand projection: that
        /// costs a people lookup and a user-data lookup per item, and none of the members handled
        /// here need either.
        /// </remarks>
        /// <param name="member">Member to read.</param>
        /// <param name="owner">User whose library is read.</param>
        /// <returns>Every value found, including duplicates.</returns>
        private IEnumerable<string> ItemBackedValues(string member, User owner)
        {
            var query = new InternalItemsQuery(owner) { IncludeItemTypes = _supportedItems, Recursive = true };

            return _libraryManager.GetItemsResult(query).Items
                .SelectMany(item => LibraryValueSource.ValuesFrom(member, item));
        }

        /// <summary>
        /// Validates definition JSON without writing anything.
        /// </summary>
        /// <param name="request">The JSON to check.</param>
        /// <response code="200">Diagnostics returned; an empty list means the definition is valid.</response>
        /// <returns>Every problem found.</returns>
        [HttpPost("Validate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<IReadOnlyList<Diagnostic>> Validate([FromBody] ValidateRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            return Ok(Inspect(request.RawJson).Diagnostics);
        }

        /// <summary>
        /// Overwrites an existing definition.
        /// </summary>
        /// <param name="fileName">On-disk name of the definition to replace, without extension.</param>
        /// <param name="request">Replacement contents and the hash the editor was given.</param>
        /// <response code="200">Saved.</response>
        /// <response code="400">The JSON is invalid, or its FileName disagrees with the route.</response>
        /// <response code="404">No definition with that name exists.</response>
        /// <response code="409">The file changed on disk since it was loaded.</response>
        /// <returns>The saved definition.</returns>
        [HttpPut("Definitions/{fileName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<DefinitionDetail>> SaveDefinition(
            [FromRoute] string fileName,
            [FromBody] SaveDefinitionRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var path = ResolvePath(fileName);
            if (path is null)
            {
                return NotFound();
            }

            var current = await System.IO.File.ReadAllTextAsync(path).ConfigureAwait(false);
            var currentHash = Hash(current);

            // A stale hash means someone edited the file while this editor was open. Overwriting would
            // discard their change with no trace, so refuse and hand back what is actually on disk.
            if (!string.IsNullOrEmpty(request.SourceHash) && !string.Equals(request.SourceHash, currentHash, StringComparison.Ordinal))
            {
                return Conflict(new ConflictResponse(
                    currentHash,
                    Pretty(current),
                    "This definition changed on disk after you opened it. Reload to see the current version, or overwrite it."));
            }

            var inspection = Inspect(request.RawJson);
            if (inspection.Dto is null || inspection.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
            {
                return BadRequest(new ValidationProblem(inspection.Diagnostics));
            }

            // FileName is the definition's identity and comes from the file on disk. Letting the body
            // change it would rename by side effect, or leave two files describing one playlist.
            if (!string.IsNullOrEmpty(inspection.Dto.FileName)
                && !string.Equals(inspection.Dto.FileName, fileName, StringComparison.Ordinal))
            {
                return BadRequest(new ValidationProblem(
                [
                    new Diagnostic(
                        "E15",
                        DiagnosticSeverity.Error,
                        $"FileName is '{inspection.Dto.FileName}' but this definition is stored as '{fileName}'. Renaming is not supported here; create a new definition and delete this one.",
                        "FileName")
                ]));
            }

            await System.IO.File.WriteAllTextAsync(path, request.RawJson).ConfigureAwait(false);

            // Log the name taken from the resolved path rather than the request value: a request value
            // could carry control characters and forge log lines.
            _logger.LogInformation("Saved playlist definition {Playlist}", Path.GetFileNameWithoutExtension(path));

            return await BuildDetailAsync(path, fileName).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a new definition file.
        /// </summary>
        /// <param name="request">Name and contents of the definition to create.</param>
        /// <response code="201">Created.</response>
        /// <response code="400">The name or the JSON is invalid.</response>
        /// <response code="409">A definition with that name already exists.</response>
        /// <returns>The created definition.</returns>
        [HttpPost("Definitions")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [SuppressMessage(
            "Security",
            "CA3003:Review code for file path injection vulnerabilities",
            Justification = "Creating a definition necessarily names a new file. The path is produced by "
                + "TryResolveNewDefinitionPath, which matches the name against a strict allowlist, rebuilds it "
                + "from that match so nothing outside the allowlist survives, and asserts the resolved absolute "
                + "path sits directly inside BasePath. The analyzer cannot follow those guards across the call.")]
        public async Task<ActionResult<DefinitionDetail>> CreateDefinition([FromBody] CreateDefinitionRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (!TryResolveNewDefinitionPath(request.FileName, out var targetPath, out var nameProblem))
            {
                return BadRequest(new ValidationProblem([nameProblem!]));
            }

            if (ResolvePath(request.FileName) is not null)
            {
                return Conflict(new ConflictResponse(
                    string.Empty,
                    string.Empty,
                    $"A definition named '{request.FileName}' already exists. Choose another name."));
            }

            var inspection = Inspect(request.RawJson);
            if (inspection.Dto is null || inspection.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
            {
                return BadRequest(new ValidationProblem(inspection.Diagnostics));
            }

            await System.IO.File.WriteAllTextAsync(targetPath, request.RawJson).ConfigureAwait(false);
            _logger.LogInformation("Created playlist definition {Playlist}", Path.GetFileNameWithoutExtension(targetPath));

            var detail = await BuildDetailAsync(targetPath, request.FileName).ConfigureAwait(false);

            return Created($"SmarterPlaylist/Definitions/{request.FileName}", detail.Value);
        }

        /// <summary>
        /// Deletes a definition, and optionally the Jellyfin playlist it built.
        /// </summary>
        /// <remarks>
        /// The Jellyfin playlist is kept unless the caller asks for it to go. It may be shared, queued or
        /// favourited by someone who never saw this page, and the definition file is the only thing the
        /// request unambiguously names — so the destructive half is opt-in, and the surviving playlist
        /// simply becomes an ordinary static list that nothing refreshes.
        /// </remarks>
        /// <param name="fileName">On-disk name of the definition to delete, without extension.</param>
        /// <param name="deletePlaylist">Whether to also delete the Jellyfin playlist the definition built.</param>
        /// <response code="204">Deleted.</response>
        /// <response code="404">No definition with that name exists.</response>
        /// <returns>No content.</returns>
        [HttpDelete("Definitions/{fileName}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> DeleteDefinition(
            [FromRoute] string fileName,
            [FromQuery] bool deletePlaylist = false)
        {
            var path = ResolvePath(fileName);
            if (path is null)
            {
                return NotFound();
            }

            // Playlist first. If removing it fails, the definition is still on disk and the whole
            // operation can be retried; doing it the other way round would leave a playlist nothing
            // points at and no way to reach it from this page.
            if (deletePlaylist)
            {
                await DeletePlaylistForAsync(path).ConfigureAwait(false);
            }

            if (!_store.Delete(fileName))
            {
                return NotFound();
            }

            // Keyed by file name, so a definition later created under the same name would otherwise
            // inherit this one's last outcome.
            _statusStore.Forget(fileName);

            _logger.LogInformation(
                "Deleted playlist definition {Playlist} (playlist removed: {Removed})",
                Path.GetFileNameWithoutExtension(path),
                deletePlaylist);

            return NoContent();
        }

        /// <summary>
        /// Removes the Jellyfin playlist a definition file points at, if it can still be resolved.
        /// </summary>
        /// <remarks>
        /// Best effort by design: a definition whose JSON is broken, whose user is gone, or that never
        /// refreshed has no resolvable playlist, and none of those may block deleting the file the user
        /// asked to delete.
        /// </remarks>
        /// <param name="path">Full path of the definition file.</param>
        /// <returns>A task that completes once any linked playlist has been removed.</returns>
        [SuppressMessage(
            "Security",
            "CA3003:Review code for file path injection vulnerabilities",
            Justification = "The path comes from enumerating BasePath, so it is not caller-controlled.")]
        private async Task DeletePlaylistForAsync(string path)
        {
            try
            {
                var raw = await System.IO.File.ReadAllTextAsync(path).ConfigureAwait(false);
                var dto = JsonSerializer.Deserialize<SmarterPlaylistDto>(raw);
                var user = dto is null || string.IsNullOrWhiteSpace(dto.User) ? null : _userManager.GetUserByName(dto.User);
                var playlist = user is null ? null : FindPlaylist(user, dto!.Id);

                if (playlist is null)
                {
                    _logger.LogInformation(
                        "No Jellyfin playlist is linked to {Playlist}, so only the definition was deleted",
                        Path.GetFileNameWithoutExtension(path));

                    return;
                }

                _libraryManager.DeleteItem(playlist, new DeleteOptions { DeleteFileLocation = true }, true);
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                _logger.LogWarning(
                    ex,
                    "Could not read {Playlist} to find its Jellyfin playlist; deleting the definition only",
                    Path.GetFileNameWithoutExtension(path));
            }
        }

        /// <summary>
        /// Turns a requested definition name into a path, or explains why it is not usable.
        /// </summary>
        /// <remarks>
        /// Creating a file inevitably means taking its name from the caller, so this is the one place a
        /// request value reaches the file system. Three independent guards, in order: the name must match
        /// a strict allowlist; the name is rebuilt from that match rather than reused, so nothing outside
        /// the allowlist can survive; and the resolved absolute path must still sit directly inside
        /// <c>BasePath</c>. The last check is what makes traversal impossible even if the first two are
        /// ever weakened.
        /// </remarks>
        /// <param name="requestedName">Name supplied by the caller.</param>
        /// <param name="path">Resolved absolute path, when the name is acceptable.</param>
        /// <param name="problem">Why the name was rejected, when it was.</param>
        /// <returns><c>true</c> when a safe path was produced.</returns>
        private bool TryResolveNewDefinitionPath(string requestedName, out string path, out Diagnostic? problem)
        {
            path = string.Empty;
            problem = null;

            var match = _safeFileName.Match(requestedName ?? string.Empty);
            if (!match.Success)
            {
                problem = new Diagnostic(
                    "E16",
                    DiagnosticSeverity.Error,
                    "The file name may contain only letters, numbers, dots, dashes and underscores, and must be 1 to 64 characters.",
                    "FileName");

                return false;
            }

            var safeName = new string([.. match.Value]);
            var basePath = Path.GetFullPath(_fileSystem.BasePath);
            var candidate = Path.GetFullPath(Path.Join(basePath, safeName + ".json"));

            if (!string.Equals(Path.GetDirectoryName(candidate), basePath.TrimEnd(Path.DirectorySeparatorChar), StringComparison.Ordinal))
            {
                problem = new Diagnostic("E16", DiagnosticSeverity.Error, "That file name is not allowed.", "FileName");

                return false;
            }

            path = candidate;

            return true;
        }

        /// <summary>
        /// Parses and validates definition JSON.
        /// </summary>
        /// <param name="rawJson">JSON to inspect.</param>
        /// <returns>The parsed definition, or <c>null</c> with a diagnostic explaining why not.</returns>
        private (SmarterPlaylistDto? Dto, IReadOnlyList<Diagnostic> Diagnostics) Inspect(string rawJson)
        {
            SmarterPlaylistDto? dto;

            try
            {
                dto = JsonSerializer.Deserialize<SmarterPlaylistDto>(rawJson);
            }
            catch (JsonException ex)
            {
                return (null, [new Diagnostic("E00", DiagnosticSeverity.Error, $"This is not valid JSON: {ex.Message}", null)]);
            }

            if (dto is null)
            {
                return (null, [new Diagnostic("E00", DiagnosticSeverity.Error, "This definition is empty.", null)]);
            }

            var diagnostics = DefinitionValidator.Validate(dto, SchemaBuilder.Build()).ToList();

            // Checked here rather than in the validator because it needs the server's user list. This
            // is what turns "silently skipped 30 minutes later" into "refused now, with the name".
            if (!string.IsNullOrWhiteSpace(dto.User) && _userManager.GetUserByName(dto.User) is null)
            {
                diagnostics.Insert(0, new Diagnostic(
                    "E17",
                    DiagnosticSeverity.Error,
                    $"No user named '{dto.User}' exists on this server, so this playlist would be skipped every refresh.",
                    "User"));
            }

            return (dto, diagnostics);
        }

        /// <summary>
        /// Finds the Jellyfin playlist a definition's stored id points at.
        /// </summary>
        /// <remarks>
        /// Ids are stored in the definition without dashes, so the dashes are stripped from the
        /// Jellyfin-side id before matching.
        /// </remarks>
        /// <param name="user">Owner of the playlists to search.</param>
        /// <param name="dtoId">Playlist id recorded in the definition, or <c>null</c> if it has never refreshed.</param>
        /// <returns>The playlist, or <c>null</c> if the definition has no id or the playlist is gone.</returns>
        private Playlist? FindPlaylist(User user, string? dtoId) =>
            dtoId is null
                ? null
                : _playlistManager.GetPlaylists(user.Id)
                    .FirstOrDefault(p => p.Id.ToString().Replace("-", string.Empty, StringComparison.Ordinal) == dtoId);

        /// <summary>
        /// Finds the file backing a definition name, without letting the name become a path.
        /// </summary>
        /// <param name="fileName">Definition name, without extension.</param>
        /// <returns>The full path, or <c>null</c> if no such definition exists.</returns>
        private string? ResolvePath(string fileName) =>
            _fileSystem.GetAllSmarterPlaylistFilePaths()
                .FirstOrDefault(p => string.Equals(Path.GetFileNameWithoutExtension(p), fileName, StringComparison.Ordinal));

        /// <summary>
        /// Builds the detail response for a definition already on disk.
        /// </summary>
        /// <param name="path">Full path of the definition file.</param>
        /// <param name="fileName">Definition name, without extension.</param>
        /// <returns>The detail response.</returns>
        [SuppressMessage(
            "Security",
            "CA3003:Review code for file path injection vulnerabilities",
            Justification = "Callers pass a path that either came from enumerating BasePath or from "
                + "TryResolveNewDefinitionPath, both of which constrain it to BasePath.")]
        private async Task<ActionResult<DefinitionDetail>> BuildDetailAsync(string path, string fileName)
        {
            var raw = await System.IO.File.ReadAllTextAsync(path).ConfigureAwait(false);
            var inspection = Inspect(raw);
            var dto = inspection.Dto ?? new SmarterPlaylistDto();
            dto.FileName = fileName;

            var summary = BuildSummary(dto, SchemaBuilder.Build(), _statusStore.GetAll());

            return new DefinitionDetail(summary, Pretty(raw), Hash(raw), inspection.Diagnostics);
        }

        /// <summary>
        /// Reads and deserializes one definition file.
        /// </summary>
        /// <param name="path">Full path of the file.</param>
        /// <returns>The definition, with its file name taken from disk.</returns>
        private static async Task<SmarterPlaylistDto> ReadDefinitionAsync(string path)
        {
            var raw = await System.IO.File.ReadAllTextAsync(path).ConfigureAwait(false);
            var dto = JsonSerializer.Deserialize<SmarterPlaylistDto>(raw)
                ?? throw new InvalidOperationException("The file is empty.");

            dto.FileName = Path.GetFileNameWithoutExtension(path);

            return dto;
        }

        /// <summary>
        /// Re-formats JSON for display, falling back to the original text if it cannot be parsed.
        /// </summary>
        /// <param name="raw">File contents.</param>
        /// <returns>Pretty-printed JSON.</returns>
        private static string Pretty(string raw)
        {
            try
            {
                using var document = JsonDocument.Parse(raw);

                return JsonSerializer.Serialize(document.RootElement, _prettyOptions);
            }
            catch (JsonException)
            {
                return raw;
            }
        }

        /// <summary>
        /// Hashes a file's contents so a later write can detect a concurrent edit.
        /// </summary>
        /// <param name="raw">File contents.</param>
        /// <returns>A hex digest.</returns>
        private static string Hash(string raw) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

        /// <summary>
        /// Builds the list row for a definition, resolving its live playlist state.
        /// </summary>
        /// <param name="dto">Definition to describe.</param>
        /// <param name="schema">Filter vocabulary used for validation.</param>
        /// <param name="statuses">Last-refresh outcomes.</param>
        /// <returns>The summary.</returns>
        private DefinitionSummary BuildSummary(
            SmarterPlaylistDto dto,
            SchemaResponse schema,
            IReadOnlyDictionary<string, RefreshStatus> statuses)
        {
            var diagnostics = DefinitionValidator.Validate(dto, schema);
            var user = string.IsNullOrWhiteSpace(dto.User) ? null : _userManager.GetUserByName(dto.User);

            var state = PlaylistState.NotCreated;
            int? itemCount = null;

            if (dto.Id is not null && user is not null)
            {
                var playlist = FindPlaylist(user, dto.Id);

                if (playlist is null)
                {
                    state = PlaylistState.Missing;
                }
                else
                {
                    state = PlaylistState.Ok;
                    var query = new InternalItemsQuery(user) { IncludeItemTypes = _supportedItems, Recursive = true };
                    itemCount = playlist.GetChildren(user, false, query).Count;
                }
            }

            var ruleCount = dto.ExpressionSets.Sum(s => s.Expressions.Count);
            var summary = string.Create(
                CultureInfo.InvariantCulture,
                $"{dto.ExpressionSets.Count} group{(dto.ExpressionSets.Count == 1 ? string.Empty : "s")}, {ruleCount} rule{(ruleCount == 1 ? string.Empty : "s")}");

            statuses.TryGetValue(dto.FileName, out var status);

            return new DefinitionSummary(
                dto.FileName,
                dto.Name,
                dto.User,
                user is not null,
                summary,
                dto.MaxItems > 0 ? dto.MaxItems : SmarterPlaylist.DefaultMaxItems,
                dto.Order.Name,
                state,
                itemCount,
                status,
                diagnostics);
        }
    }
}
