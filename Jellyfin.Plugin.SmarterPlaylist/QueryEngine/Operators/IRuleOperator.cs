using System;
using System.Collections.Generic;
using LinqExpression = System.Linq.Expressions.Expression;

namespace Jellyfin.Plugin.SmarterPlaylist.QueryEngine.Operators
{
    /// <summary>
    /// One comparison a rule can name, and everything the rest of the plugin needs to know about it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This replaced resolving an operator by reflecting for a method of that name on the member's CLR
    /// type. Reflection meant the vocabulary was never declared anywhere: it was whatever
    /// <see cref="string"/> and <see cref="System.Collections.ObjectModel.Collection{T}"/> happened to
    /// expose, which included mutating methods. A rule naming <c>Remove</c> on a list member compiled
    /// into a predicate that removed the value while testing it.
    /// </para>
    /// <para>
    /// Implementations are the single source for all three consumers: the engine compiles from
    /// <see cref="Build"/>, the schema advertises what <see cref="AppliesTo"/> permits, and validation
    /// reports what <see cref="ValidateValue"/> rejects. Adding an operator here adds it everywhere.
    /// </para>
    /// </remarks>
    public interface IRuleOperator
    {
        /// <summary>
        /// Gets the name a rule writes in its <c>Operator</c> field.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the member kinds this operator can be applied to.
        /// </summary>
        IReadOnlyList<MemberKind> AppliesTo { get; }

        /// <summary>
        /// Gets how many values this operator reads from the rule's target value.
        /// </summary>
        ValueArity Arity { get; }

        /// <summary>
        /// Gets a short description of the operator's behaviour, or <c>null</c> when the name says it.
        /// </summary>
        /// <remarks>
        /// Surfaced in the configuration page's help text. Anything a user would otherwise get wrong --
        /// case sensitivity, whole-element versus substring matching -- belongs here.
        /// </remarks>
        string? Notes { get; }

        /// <summary>
        /// Builds the expression tree implementing this operator.
        /// </summary>
        /// <param name="context">The member under test and the value to compare it against.</param>
        /// <returns>A boolean-valued expression.</returns>
        LinqExpression Build(RuleOperatorContext context);

        /// <summary>
        /// Checks a target value before the rule is compiled.
        /// </summary>
        /// <param name="targetValue">Value as written in the rule.</param>
        /// <param name="kind">Kind of the member the rule targets.</param>
        /// <returns>The problem found, or <c>null</c> when the value is usable.</returns>
        RuleValueProblem? ValidateValue(string targetValue, MemberKind kind);
    }
}
