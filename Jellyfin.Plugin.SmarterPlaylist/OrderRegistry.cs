using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.SmarterPlaylist
{
    /// <summary>
    /// Every sort order a definition may name, and how to construct it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The single source of the order vocabulary, for the same reason
    /// <see cref="QueryEngine.Operators.OperatorRegistry"/> is the single source of the operator one.
    /// Adding an order previously meant three edits in three files: the class, an arm in
    /// <see cref="SmarterPlaylist"/>'s switch, and an entry in the schema's hand-written array. Nothing
    /// failed at compile time if you missed either list, and the two failures were both silent —
    /// a missing switch arm made the page offer an order that quietly fell back to library order, and
    /// a missing array entry hid a working order from the page entirely.
    /// </para>
    /// <para>
    /// Order matters: this is the sequence the configuration page lists them in, so
    /// <see cref="NoOrder"/> stays first as the default.
    /// </para>
    /// </remarks>
    public static class OrderRegistry
    {
        /// <summary>
        /// Every order, paired with a factory rather than an instance.
        /// </summary>
        /// <remarks>
        /// A factory keeps the previous behaviour of one <see cref="Order"/> per playlist exactly.
        /// Today's orders are all stateless and could be shared, but making that a requirement of the
        /// registry would turn any future stateful order into a race between concurrent refreshes.
        /// </remarks>
        private static readonly (string Name, Func<Order> Create)[] _all =
        [
            (NoOrder.OrderName, () => new NoOrder()),
            (PremiereDateOrder.OrderName, () => new PremiereDateOrder()),
            (PremiereDateOrderDesc.OrderName, () => new PremiereDateOrderDesc()),
            (SeriesEpisodeOrder.OrderName, () => new SeriesEpisodeOrder())
        ];

        /// <summary>
        /// Gets the name of every registered order, in the order they are offered.
        /// </summary>
        public static IReadOnlyList<string> Names { get; } = [.. _all.Select(o => o.Name)];

        /// <summary>
        /// Builds the order a definition names.
        /// </summary>
        /// <remarks>
        /// An unrecognised name falls back to <see cref="NoOrder"/> rather than throwing, which is the
        /// long-standing behaviour: a typo in one definition's order should leave its items unsorted,
        /// not fail its refresh. Validation reports the same case as a warning, so it is visible
        /// without being fatal.
        /// </remarks>
        /// <param name="name">Order name from the definition, matched case-sensitively.</param>
        /// <returns>The matching order, or a <see cref="NoOrder"/>.</returns>
        public static Order Resolve(string? name)
        {
            foreach (var (registered, create) in _all)
            {
                if (string.Equals(registered, name, StringComparison.Ordinal))
                {
                    return create();
                }
            }

            return new NoOrder();
        }

        /// <summary>
        /// Reports whether a name is a registered order.
        /// </summary>
        /// <param name="name">Order name to check.</param>
        /// <returns><c>true</c> when the name resolves to something other than the fallback.</returns>
        public static bool IsKnown(string? name) =>
            name is not null && Names.Contains(name, StringComparer.Ordinal);
    }
}
