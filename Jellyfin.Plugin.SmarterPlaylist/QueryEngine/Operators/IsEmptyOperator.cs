using System;
using System.Collections.Generic;
using LinqExpression = System.Linq.Expressions.Expression;

namespace Jellyfin.Plugin.SmarterPlaylist.QueryEngine.Operators
{
    /// <summary>
    /// Tests whether a text member is blank or a list member holds nothing.
    /// </summary>
    /// <remarks>
    /// This is how a library gets audited: items missing an official rating, episodes with no series
    /// name, films nobody tagged. Previously unexpressible -- <c>Equal</c> against an empty string
    /// covered only the text case, and nothing at all covered the list case, because an operator had
    /// to be a one-argument method and this takes no argument.
    /// </remarks>
    /// <param name="name">Operator name as written in a rule.</param>
    /// <param name="negate">Whether the result is inverted.</param>
    public sealed class IsEmptyOperator(string name, bool negate) : RuleOperator
    {
        private static readonly MemberKind[] _appliesTo = [MemberKind.Text, MemberKind.TextEnum, MemberKind.TextList];

        /// <inheritdoc />
        public override string Name { get; } = name;

        /// <inheritdoc />
        public override IReadOnlyList<MemberKind> AppliesTo => _appliesTo;

        /// <inheritdoc />
        public override ValueArity Arity => ValueArity.None;

        /// <inheritdoc />
        public override string? Notes { get; } = negate
            ? "Takes no value. Holds when the member has some text, or a list member holds at least one element."
            : "Takes no value. Holds when the member is blank, or a list member holds nothing.";

        /// <inheritdoc />
        public override LinqExpression Build(RuleOperatorContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            LinqExpression hasContent;

            if (context.Kind == MemberKind.TextList)
            {
                hasContent = LinqExpression.Call(AnyWithoutPredicate, context.Member);
            }
            else
            {
                var isNullOrEmpty = typeof(string).GetMethod(nameof(string.IsNullOrEmpty), [typeof(string)])
                    ?? throw new MissingMethodException(nameof(String), nameof(string.IsNullOrEmpty));

                hasContent = LinqExpression.Not(LinqExpression.Call(isNullOrEmpty, context.Member));
            }

            return negate ? hasContent : LinqExpression.Not(hasContent);
        }
    }
}
