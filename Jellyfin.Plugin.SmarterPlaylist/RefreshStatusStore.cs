using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Jellyfin.Plugin.SmarterPlaylist
{
    /// <summary>
    /// In-memory implementation of <see cref="IRefreshStatusStore"/>, registered as a singleton.
    /// </summary>
    /// <remarks>
    /// The scheduled task writes while the API reads, so the backing store is concurrent.
    /// </remarks>
    public sealed class RefreshStatusStore : IRefreshStatusStore
    {
        private readonly ConcurrentDictionary<string, RefreshStatus> _statuses =
            new(StringComparer.OrdinalIgnoreCase);

        /// <inheritdoc />
        public void Record(RefreshStatus status)
        {
            ArgumentNullException.ThrowIfNull(status);

            _statuses[status.FileName] = status;
        }

        /// <inheritdoc />
        public RefreshStatus? Get(string fileName)
        {
            return _statuses.TryGetValue(fileName, out var status) ? status : null;
        }

        /// <inheritdoc />
        public IReadOnlyDictionary<string, RefreshStatus> GetAll()
        {
            return new Dictionary<string, RefreshStatus>(_statuses, StringComparer.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        public void Forget(string fileName)
        {
            _statuses.TryRemove(fileName, out _);
        }
    }
}
