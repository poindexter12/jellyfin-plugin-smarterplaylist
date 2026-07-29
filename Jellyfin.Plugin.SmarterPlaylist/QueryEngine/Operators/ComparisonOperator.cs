using System.Collections.Generic;
using System.Linq.Expressions;
using LinqExpression = System.Linq.Expressions.Expression;

namespace Jellyfin.Plugin.SmarterPlaylist.QueryEngine.Operators
{
    /// <summary>
    /// A binary comparison such as <c>Equal</c> or <c>GreaterThan</c>.
    /// </summary>
    /// <remarks>
    /// These were previously resolved by parsing the operator name as an <see cref="ExpressionType"/>,
    /// which accepted every member of that enum -- including <c>Assign</c>, <c>AddAssign</c> and the
    /// rest of the mutating set -- for any member type. Only the six below were ever advertised, and
    /// only they are accepted now.
    /// </remarks>
    /// <param name="name">Operator name as written in a rule.</param>
    /// <param name="expressionType">Binary operation to build.</param>
    /// <param name="appliesTo">Member kinds the comparison is offered for.</param>
    public sealed class ComparisonOperator(string name, ExpressionType expressionType, params MemberKind[] appliesTo)
        : RuleOperator
    {
        /// <inheritdoc />
        public override string Name { get; } = name;

        /// <inheritdoc />
        public override IReadOnlyList<MemberKind> AppliesTo { get; } = appliesTo;

        /// <inheritdoc />
        public override LinqExpression Build(RuleOperatorContext context)
        {
            System.ArgumentNullException.ThrowIfNull(context);

            return LinqExpression.MakeBinary(expressionType, context.Member, Constant(context.TargetValue, context.MemberType));
        }
    }
}
