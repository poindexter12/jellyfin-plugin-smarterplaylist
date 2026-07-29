using System;
using System.Collections.Generic;
using System.Linq;
using LinqExpression = System.Linq.Expressions.Expression;

namespace Jellyfin.Plugin.SmarterPlaylist.QueryEngine.Operators
{
    /// <summary>
    /// Tests a member against several candidate values at once.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The alternative was one rule group per value, because rules within a group are combined with
    /// AND. Selecting three genres therefore meant three near-identical groups, and the configuration
    /// page had no way to show that as one choice.
    /// </para>
    /// <para>
    /// On a text member the whole value must equal one of the candidates. On a list member the rule
    /// holds when any element equals any candidate, which is the same whole-element, case-sensitive
    /// comparison <c>Contains</c> uses. Write <c>\,</c> for a value containing a comma.
    /// </para>
    /// </remarks>
    /// <param name="name">Operator name as written in a rule.</param>
    /// <param name="negate">Whether the result is inverted.</param>
    public sealed class AnyOfOperator(string name, bool negate) : RuleOperator
    {
        private static readonly MemberKind[] _appliesTo = [MemberKind.Text, MemberKind.TextEnum, MemberKind.TextList];

        /// <inheritdoc />
        public override string Name { get; } = name;

        /// <inheritdoc />
        public override IReadOnlyList<MemberKind> AppliesTo => _appliesTo;

        /// <inheritdoc />
        public override ValueArity Arity => ValueArity.List;

        /// <inheritdoc />
        public override string? Notes { get; } = negate
            ? "A comma-separated list. Holds when the member matches none of them. Comparisons are case-sensitive; write \\, for a literal comma."
            : "A comma-separated list, such as Comedy,Drama. Holds when the member matches any of them. Comparisons are case-sensitive; write \\, for a literal comma.";

        /// <inheritdoc />
        public override LinqExpression Build(RuleOperatorContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var values = LinqExpression.Constant(
                RuleValueList.Split(context.TargetValue).ToArray(),
                typeof(IEnumerable<string>));
            var comparer = LinqExpression.Constant(StringComparer.Ordinal, typeof(IEqualityComparer<string>));

            var call = context.Kind == MemberKind.TextList
                ? AnyElement(context.Member, element => LinqExpression.Call(ContainsWithComparer, values, element, comparer))
                : LinqExpression.Call(ContainsWithComparer, values, context.Member, comparer);

            return negate ? LinqExpression.Not(call) : call;
        }

        /// <inheritdoc />
        public override RuleValueProblem? ValidateValue(string targetValue, MemberKind kind)
        {
            return RuleValueList.Split(targetValue).Any(string.IsNullOrEmpty)
                ? new RuleValueProblem(
                    "E17",
                    $"{Name} was given an empty value in '{targetValue}'. Remove the stray comma, or write \\, for a literal one.")
                : null;
        }
    }
}
