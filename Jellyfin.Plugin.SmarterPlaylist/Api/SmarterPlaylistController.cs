using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
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
    /// Read-only API backing the plugin's configuration page.
    /// </summary>
    [ApiController]
    [Authorize(Policy = Policies.RequiresElevation)]
    [Route("SmarterPlaylist")]
    [Produces(MediaTypeNames.Application.Json)]
    public class SmarterPlaylistController : ControllerBase
    {
        private static readonly BaseItemKind[] _supportedItems =
            [BaseItemKind.Audio, BaseItemKind.Episode, BaseItemKind.Movie];

        private static readonly JsonSerializerOptions _prettyOptions = new() { WriteIndented = true };

        private readonly ISmarterPlaylistFileSystem _fileSystem;
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger<Plugin> _logger;
        private readonly IPlaylistManager _playlistManager;
        private readonly IRefreshStatusStore _statusStore;
        private readonly IUserManager _userManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="SmarterPlaylistController"/> class.
        /// </summary>
        /// <param name="fileSystem">Locates definition files.</param>
        /// <param name="libraryManager">Unused directly, reserved for preview endpoints.</param>
        /// <param name="logger">Logger for read failures.</param>
        /// <param name="playlistManager">Resolves the live playlist behind each definition.</param>
        /// <param name="statusStore">Last-refresh outcomes.</param>
        /// <param name="userManager">Resolves the user each definition names.</param>
        public SmarterPlaylistController(
            ISmarterPlaylistFileSystem fileSystem,
            ILibraryManager libraryManager,
            ILogger<Plugin> logger,
            IPlaylistManager playlistManager,
            IRefreshStatusStore statusStore,
            IUserManager userManager)
        {
            _fileSystem = fileSystem;
            _libraryManager = libraryManager;
            _logger = logger;
            _playlistManager = playlistManager;
            _statusStore = statusStore;
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
                var playlist = _playlistManager.GetPlaylists(user.Id)
                    .FirstOrDefault(p => p.Id.ToString().Replace("-", string.Empty, StringComparison.Ordinal) == dto.Id);

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
