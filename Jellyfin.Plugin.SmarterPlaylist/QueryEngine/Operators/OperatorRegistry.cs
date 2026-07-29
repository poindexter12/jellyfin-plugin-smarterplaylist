using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Jellyfin.Plugin.SmarterPlaylist.QueryEngine.Operators
{
    /// <summary>
    /// Every operator a rule may name, and which members each one applies to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The single source of the filter vocabulary. The engine compiles what it finds here, the schema
    /// advertises it, and validation checks against it, so the three cannot disagree about what a rule
    /// is allowed to say. They previously each held their own answer: the schema listed operators per
    /// member kind, the validator carried its own special cases, and the engine accepted anything that
    /// happened to be a method on the member's CLR type -- a far larger set than either of the others
    /// knew about, and one that included methods which mutated the item being tested.
    /// </para>
    /// <para>
    /// Order matters. Operators appear in the configuration page in the order listed here, so the
    /// long-standing ones come first and the additions follow, rather than sorting alphabetically and
    /// scattering the familiar choices.
    /// </para>
    /// </remarks>
    public static class OperatorRegistry
    {
        private static readonly IRuleOperator[] _all =
        [
            // Comparisons. Dates are Unix seconds by the time these run, so they order correctly.
            new ComparisonOperator("Equal", ExpressionType.Equal, MemberKind.Text, MemberKind.TextEnum, MemberKind.Number, MemberKind.Date, MemberKind.Boolean),
            new ComparisonOperator("NotEqual", ExpressionType.NotEqual, MemberKind.Text, MemberKind.TextEnum, MemberKind.Number, MemberKind.Date, MemberKind.Boolean),
            new ComparisonOperator("GreaterThan", ExpressionType.GreaterThan, MemberKind.Number, MemberKind.Date),
            new ComparisonOperator("GreaterThanOrEqual", ExpressionType.GreaterThanOrEqual, MemberKind.Number, MemberKind.Date),
            new ComparisonOperator("LessThan", ExpressionType.LessThan, MemberKind.Number, MemberKind.Date),
            new ComparisonOperator("LessThanOrEqual", ExpressionType.LessThanOrEqual, MemberKind.Number, MemberKind.Date),

            // String tests. Equals duplicates Equal for text and predates it; both are kept because
            // definitions in the wild use each.
            new StringMethodOperator("Equals", "Case-sensitive. Equal does the same thing.", MemberKind.Text, MemberKind.TextEnum),
            new StringMethodOperator("Contains", "Matches a substring and is case-sensitive.", MemberKind.Text),
            new StringMethodOperator("StartsWith", "Case-sensitive.", MemberKind.Text),
            new StringMethodOperator("EndsWith", "Case-sensitive.", MemberKind.Text),

            // List membership.
            new ListContainsOperator("Contains"),

            // Patterns.
            new RegexOperator("MatchRegex", false),
            new RegexOperator("NotMatchRegex", true),

            // Added with the operator registry: none of these is a one-argument method on the member's
            // type, so none could be expressed while operators were resolved by reflection.
            new BetweenOperator("Between", false),
            new BetweenOperator("NotBetween", true),
            new AnyOfOperator("AnyOf", false),
            new AnyOfOperator("NoneOf", true),
            new ContainsIgnoreCaseOperator(),
            new ListContainsOperator("NotContains", negate: true),
            new IsEmptyOperator("IsEmpty", false),
            new IsEmptyOperator("IsNotEmpty", true)
        ];

        /// <summary>
        /// Gets every registered operator, in the order they are offered.
        /// </summary>
        public static IReadOnlyList<IRuleOperator> All => _all;

        /// <summary>
        /// Finds the operator serving a name for a given member kind.
        /// </summary>
        /// <remarks>
        /// Keyed on both, because one name can mean different things for different members:
        /// <c>Contains</c> is a substring test on text and a whole-element test on a list.
        /// </remarks>
        /// <param name="name">Operator name from the rule.</param>
        /// <param name="kind">Kind of the member being tested.</param>
        /// <returns>The operator, or <c>null</c> when the name is not valid for that kind.</returns>
        public static IRuleOperator? Find(string name, MemberKind kind) =>
            _all.FirstOrDefault(o => string.Equals(o.Name, name, StringComparison.Ordinal) && o.AppliesTo.Contains(kind));

        /// <summary>
        /// Lists the operators offered for a member kind, in registration order.
        /// </summary>
        /// <param name="kind">Kind to list for.</param>
        /// <returns>The operators, which is what the schema advertises.</returns>
        public static IReadOnlyList<IRuleOperator> ForKind(MemberKind kind) =>
            [.. _all.Where(o => o.AppliesTo.Contains(kind))];

        /// <summary>
        /// Lists the operator names offered for a member kind.
        /// </summary>
        /// <param name="kind">Kind to list for.</param>
        /// <returns>The names, which is what a rule's <c>Operator</c> field may hold.</returns>
        public static IReadOnlyList<string> NamesForKind(MemberKind kind) =>
            [.. ForKind(kind).Select(o => o.Name)];
    }
}
