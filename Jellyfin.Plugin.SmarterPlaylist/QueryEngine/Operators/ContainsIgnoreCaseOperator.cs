using System;
using System.Collections.Generic;
using LinqExpression = System.Linq.Expressions.Expression;

namespace Jellyfin.Plugin.SmarterPlaylist.QueryEngine.Operators
{
    /// <summary>
    /// The case-insensitive counterpart to <c>Contains</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Case sensitivity was the schema's most repeated caveat, and the only way around it was a regular
    /// expression -- which meant users reached for <c>MatchRegex</c> to express "comedy, whatever the
    /// capitalisation" and inherited pattern syntax they did not want.
    /// </para>
    /// <para>
    /// This mirrors whatever <c>Contains</c> means for the member's kind: substring on a text member,
    /// whole element on a list member. Doing anything else would make the pair inconsistent in a way
    /// only the source would explain.
    /// </para>
    /// </remarks>
    public sealed class ContainsIgnoreCaseOperator : RuleOperator
    {
        private static readonly MemberKind[] _appliesTo = [MemberKind.Text, MemberKind.TextList];

        /// <inheritdoc />
        public override string Name => "ContainsIgnoreCase";

        /// <inheritdoc />
        public override IReadOnlyList<MemberKind> AppliesTo => _appliesTo;

        /// <inheritdoc />
        public override string? Notes =>
            "Ignores case. Matches a substring of a text member, and a whole element of a list member, mirroring Contains.";

        /// <inheritdoc />
        public override LinqExpression Build(RuleOperatorContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var value = LinqExpression.Constant(context.TargetValue);

            if (context.Kind == MemberKind.TextList)
            {
                var comparer = LinqExpression.Constant(StringComparer.OrdinalIgnoreCase, typeof(IEqualityComparer<string>));

                return LinqExpression.Call(ContainsWithComparer, context.Member, value, comparer);
            }

            var method = typeof(string).GetMethod(nameof(string.Contains), [typeof(string), typeof(StringComparison)])
                ?? throw new MissingMethodException(nameof(String), nameof(string.Contains));

            return LinqExpression.Call(
                context.Member,
                method,
                value,
                LinqExpression.Constant(StringComparison.OrdinalIgnoreCase));
        }

        /// <inheritdoc />
        public override RuleValueProblem? ValidateValue(string targetValue, MemberKind kind) =>
            string.IsNullOrEmpty(targetValue)
                ? new RuleValueProblem("E13", $"{Name} needs a value to look for.")
                : null;
    }
}
