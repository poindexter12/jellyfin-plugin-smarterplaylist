using System;
using System.Collections.Generic;
using LinqExpression = System.Linq.Expressions.Expression;

namespace Jellyfin.Plugin.SmarterPlaylist.QueryEngine.Operators
{
    /// <summary>
    /// A case-sensitive <see cref="string"/> test such as <c>Contains</c> or <c>StartsWith</c>.
    /// </summary>
    /// <remarks>
    /// The method is resolved once, from <see cref="string"/>, against the exact signature wanted.
    /// Previously the operator name was passed to <c>GetMethod</c> on whatever the member's type was,
    /// so the accepted vocabulary varied by member and included anything returning <see cref="bool"/>.
    /// </remarks>
    /// <param name="name">Operator name, which is also the method name on <see cref="string"/>.</param>
    /// <param name="notes">Behaviour worth stating in the schema, or <c>null</c>.</param>
    /// <param name="appliesTo">Member kinds the test is offered for.</param>
    public sealed class StringMethodOperator(string name, string? notes, params MemberKind[] appliesTo) : RuleOperator
    {
        /// <inheritdoc />
        public override string Name { get; } = name;

        /// <inheritdoc />
        public override IReadOnlyList<MemberKind> AppliesTo { get; } = appliesTo;

        /// <inheritdoc />
        public override string? Notes => notes;

        /// <inheritdoc />
        public override LinqExpression Build(RuleOperatorContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var method = typeof(string).GetMethod(Name, [typeof(string)])
                ?? throw new MissingMethodException(nameof(String), Name);

            return LinqExpression.Call(context.Member, method, LinqExpression.Constant(context.TargetValue));
        }
    }
}
