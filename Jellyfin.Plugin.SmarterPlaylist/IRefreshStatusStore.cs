using System.Collections.Generic;

namespace Jellyfin.Plugin.SmarterPlaylist
{
    /// <summary>
    /// Records the outcome of each definition's last refresh.
    /// </summary>
    /// <remarks>
    /// Held in memory only. Status is therefore unknown after a server restart until the next task run,
    /// which the UI shows explicitly rather than guessing. The deliberate alternative — writing status
    /// back into the user's definition file — was rejected because it would rewrite hand-authored JSON
    /// every 30 minutes.
    /// </remarks>
    public interface IRefreshStatusStore
    {
        /// <summary>
        /// Records the outcome of a refresh, replacing any previous entry for the same definition.
        /// </summary>
        /// <param name="status">Outcome to record.</param>
        void Record(RefreshStatus status);

        /// <summary>
        /// Gets the last recorded outcome for a definition.
        /// </summary>
        /// <param name="fileName">On-disk name of the definition, without extension.</param>
        /// <returns>The last outcome, or <c>null</c> if it has not run since the server started.</returns>
        RefreshStatus? Get(string fileName);

        /// <summary>
        /// Gets every recorded outcome, keyed by definition file name.
        /// </summary>
        /// <returns>A snapshot of all known outcomes.</returns>
        IReadOnlyDictionary<string, RefreshStatus> GetAll();

        /// <summary>
        /// Drops the recorded outcome for a definition that no longer exists.
        /// </summary>
        /// <remarks>
        /// Entries are keyed by file name, so without this a definition deleted and then re-created under
        /// the same name would show the deleted one's last outcome — including its failure — until the
        /// next task run.
        /// </remarks>
        /// <param name="fileName">On-disk name of the definition, without extension.</param>
        void Forget(string fileName);
    }
}
