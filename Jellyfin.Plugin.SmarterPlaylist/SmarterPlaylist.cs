using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.SmarterPlaylist.QueryEngine;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.SmarterPlaylist
{
    /// <summary>
    /// A playlist definition prepared for evaluation against a library.
    /// </summary>
    /// <remarks>
    /// This is the runtime counterpart of <see cref="SmarterPlaylistDto"/>: the on-disk strings are
    /// resolved into a concrete <see cref="Order"/> and the rules are normalized ready to compile.
    /// </remarks>
    public class SmarterPlaylist
    {
        /// <summary>
        /// Number of items included when the definition does not specify a limit.
        /// </summary>
        public const int DefaultMaxItems = 1000;

        /// <summary>
        /// Initializes a new instance of the <see cref="SmarterPlaylist"/> class from an on-disk definition.
        /// </summary>
        /// <param name="dto">Definition loaded from the playlist's JSON file.</param>
        public SmarterPlaylist(SmarterPlaylistDto dto)
        {
            ArgumentNullException.ThrowIfNull(dto);

            Id = dto.Id;
            Name = dto.Name;
            FileName = dto.FileName;
            User = dto.User;
            ExpressionSets = Engine.NormalizeRuleSets(dto.ExpressionSets);
            MaxItems = dto.MaxItems > 0 ? dto.MaxItems : DefaultMaxItems;

            Order = dto.Order.Name switch
            {
                PremiereDateOrder.OrderName => new PremiereDateOrder(),
                PremiereDateOrderDesc.OrderName => new PremiereDateOrderDesc(),
                SeriesEpisodeOrder.OrderName => new SeriesEpisodeOrder(),
                _ => new NoOrder(),
            };

            ReferencedMembers = ExpressionSets
                .SelectMany(set => set.Expressions)
                .Select(rule => rule.MemberName)
                .ToHashSet(StringComparer.Ordinal);
        }

        /// <summary>
        /// Gets or sets the id of the generated Jellyfin playlist, or <c>null</c> before it is first created.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Gets or sets the playlist name as it appears in Jellyfin.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the definition's own file name, without the <c>.json</c> extension.
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Gets or sets the name of the user the playlist is generated for.
        /// </summary>
        public string User { get; set; }

        /// <summary>
        /// Gets the rule sets that select items, OR'd together.
        /// </summary>
        public Collection<ExpressionSet> ExpressionSets { get; }

        /// <summary>
        /// Gets or sets the maximum number of items to include.
        /// </summary>
        public int MaxItems { get; set; }

        /// <summary>
        /// Gets or sets the sort order applied to matched items.
        /// </summary>
        public Order Order { get; set; }

        /// <summary>
        /// Gets the members this playlist's rules actually read.
        /// </summary>
        /// <remarks>
        /// Projecting an item is cheap except for its credits and its play state, which are a lookup
        /// each, per item, per definition. Knowing up front which members are read lets both of those
        /// be skipped for the definitions that never mention them.
        /// </remarks>
        public IReadOnlySet<string> ReferencedMembers { get; }

        /// <summary>
        /// Selects the items matching this playlist's rules, in the configured order.
        /// </summary>
        /// <param name="items">Candidate library items to filter.</param>
        /// <param name="libraryManager">Library manager used to project items into operands.</param>
        /// <param name="userDataManager">User data manager used to resolve play state.</param>
        /// <param name="user">User the playlist is generated for.</param>
        /// <returns>
        /// The ids of the matching items, sorted by <see cref="Order"/> and capped at
        /// <see cref="MaxItems"/>, together with how many matched before the cap.
        /// </returns>
        public FilterResult FilterPlaylistItems(
            IEnumerable<BaseItem> items,
            ILibraryManager libraryManager,
            IUserDataManager userDataManager,
            User user)
        {
            ArgumentNullException.ThrowIfNull(items);

            var compiledRules = CompileRuleSets();
            var results = new List<BaseItem>();

            foreach (var item in items)
            {
                var operand = OperandFactory.GetMediaType(libraryManager, userDataManager, item, user, ReferencedMembers);
                if (compiledRules.Any(set => set.All(rule => rule(operand))))
                {
                    results.Add(item);
                }
            }

            var ids = Order.OrderBy(results).Take(MaxItems).Select(x => x.Id).ToList();

            return new FilterResult(ids, results.Count);
        }

        /// <summary>
        /// Compiles every rule set into predicates over an <see cref="Operand"/>.
        /// </summary>
        /// <returns>One list of predicates per rule set.</returns>
        private List<List<Func<Operand, bool>>> CompileRuleSets()
        {
            var compiledRuleSets = new List<List<Func<Operand, bool>>>();

            foreach (var set in ExpressionSets)
            {
                compiledRuleSets.Add(set.Expressions.Select(Engine.CompileRule<Operand>).ToList());
            }

            return compiledRuleSets;
        }
    }
}
