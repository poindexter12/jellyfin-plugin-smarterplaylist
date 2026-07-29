using System;
using System.Collections.Generic;
using LinqExpression = System.Linq.Expressions.Expression;

namespace Jellyfin.Plugin.SmarterPlaylist.QueryEngine.Operators
{
    /// <summary>
    /// Tests whether a list member holds an element equal to the target value.
    /// </summary>
    /// <remarks>
    /// This matches a whole element, not a substring, which is why <c>MatchRegex</c> exists alongside
    /// it. The comparer is stated rather than left to the default so the case sensitivity is visible
    /// here rather than inherited from whichever collection type the member happens to use.
    /// </remarks>
    /// <param name="name">Operator name as written in a rule.</param>
    /// <param name="negate">Whether the result is inverted.</param>
    /// <param name="ignoreCase">Whether elements are compared case-insensitively.</param>
    public sealed class ListContainsOperator(string name, bool negate = false, bool ignoreCase = false) : RuleOperator
    {
        private static readonly MemberKind[] _appliesTo = [MemberKind.TextList];

        /// <inheritdoc />
        public override string Name { get; } = name;

        /// <inheritdoc />
        public override IReadOnlyList<MemberKind> AppliesTo => _appliesTo;

        /// <inheritdoc />
        public override string? Notes { get; } = negate
            ? "Holds when no element equals the value. Matches whole elements, not substrings."
            : ignoreCase
                ? "Matches a whole element, ignoring case."
                : "Matches a whole element exactly and is case-sensitive. Use MatchRegex for partial matches.";

        /// <inheritdoc />
        public override LinqExpression Build(RuleOperatorContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var comparer = LinqExpression.Constant(
                ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal,
                typeof(IEqualityComparer<string>));

            var call = LinqExpression.Call(
                ContainsWithComparer,
                context.Member,
                LinqExpression.Constant(context.TargetValue),
                comparer);

            return negate ? LinqExpression.Not(call) : call;
        }

        /// <inheritdoc />
        public override RuleValueProblem? ValidateValue(string targetValue, MemberKind kind) =>
            string.IsNullOrEmpty(targetValue)
                ? new RuleValueProblem("E13", $"{Name} needs a value to look for.")
                : null;
    }
}
