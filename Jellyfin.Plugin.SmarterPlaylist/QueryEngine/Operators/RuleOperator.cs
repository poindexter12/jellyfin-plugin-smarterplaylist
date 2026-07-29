using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using LinqExpression = System.Linq.Expressions.Expression;

namespace Jellyfin.Plugin.SmarterPlaylist.QueryEngine.Operators
{
    /// <summary>
    /// Shared plumbing for <see cref="IRuleOperator"/> implementations.
    /// </summary>
    public abstract class RuleOperator : IRuleOperator
    {
        /// <summary>
        /// The <see cref="Enumerable.Any{TSource}(IEnumerable{TSource})"/> overload taking no predicate.
        /// </summary>
        private static readonly MethodInfo _anyWithoutPredicate = typeof(Enumerable).GetMethods()
            .Single(m => m.Name == nameof(Enumerable.Any) && m.GetParameters().Length == 1)
            .MakeGenericMethod(typeof(string));

        /// <summary>
        /// The <see cref="Enumerable.Any{TSource}(IEnumerable{TSource}, Func{TSource, bool})"/> overload.
        /// </summary>
        private static readonly MethodInfo _anyWithPredicate = typeof(Enumerable).GetMethods()
            .Single(m => m.Name == nameof(Enumerable.Any) && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(string));

        /// <summary>
        /// The <see cref="Enumerable.Contains{TSource}(IEnumerable{TSource}, TSource, IEqualityComparer{TSource})"/> overload.
        /// </summary>
        private static readonly MethodInfo _containsWithComparer = typeof(Enumerable).GetMethods()
            .Single(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 3)
            .MakeGenericMethod(typeof(string));

        /// <inheritdoc />
        public abstract string Name { get; }

        /// <inheritdoc />
        public abstract IReadOnlyList<MemberKind> AppliesTo { get; }

        /// <inheritdoc />
        public virtual ValueArity Arity => ValueArity.Single;

        /// <inheritdoc />
        public virtual string? Notes => null;

        /// <summary>
        /// Gets the <c>Any()</c> overload testing whether a sequence has any element.
        /// </summary>
        protected static MethodInfo AnyWithoutPredicate => _anyWithoutPredicate;

        /// <summary>
        /// Gets the <c>Any(predicate)</c> overload.
        /// </summary>
        protected static MethodInfo AnyWithPredicate => _anyWithPredicate;

        /// <summary>
        /// Gets the <c>Contains(value, comparer)</c> overload.
        /// </summary>
        protected static MethodInfo ContainsWithComparer => _containsWithComparer;

        /// <inheritdoc />
        public abstract LinqExpression Build(RuleOperatorContext context);

        /// <inheritdoc />
        public virtual RuleValueProblem? ValidateValue(string targetValue, MemberKind kind) => null;

        /// <summary>
        /// Converts a target value to the member's CLR type, as a constant expression.
        /// </summary>
        /// <param name="value">Value to convert.</param>
        /// <param name="memberType">Type to convert to.</param>
        /// <returns>A constant expression of <paramref name="memberType"/>.</returns>
        protected static LinqExpression Constant(string value, Type memberType) =>
            LinqExpression.Constant(Convert.ChangeType(value, memberType, CultureInfo.InvariantCulture), memberType);

        /// <summary>
        /// Builds <c>source.Any(element =&gt; predicate(element))</c> over a sequence of strings.
        /// </summary>
        /// <param name="source">Expression yielding the sequence.</param>
        /// <param name="predicate">Builds the per-element test from the element expression.</param>
        /// <returns>A boolean-valued expression.</returns>
        protected static LinqExpression AnyElement(LinqExpression source, Func<LinqExpression, LinqExpression> predicate)
        {
            ArgumentNullException.ThrowIfNull(predicate);

            var element = LinqExpression.Parameter(typeof(string), "element");
            var lambda = LinqExpression.Lambda<Func<string, bool>>(predicate(element), element);

            return LinqExpression.Call(AnyWithPredicate, source, lambda);
        }
    }
}
