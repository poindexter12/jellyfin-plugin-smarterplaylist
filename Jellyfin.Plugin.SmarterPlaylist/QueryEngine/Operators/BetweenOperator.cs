using System;
using System.Collections.Generic;
using System.Globalization;
using LinqExpression = System.Linq.Expressions.Expression;

namespace Jellyfin.Plugin.SmarterPlaylist.QueryEngine.Operators
{
    /// <summary>
    /// Tests whether a number or date falls within an inclusive range.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Expressible only because operators are declared rather than reflected: there is no one-argument
    /// method on <see cref="double"/> that means "between", so the previous engine could not have
    /// offered this at all. Two rules in a group achieved the same thing, which meant a range could
    /// not be one row in the configuration page.
    /// </para>
    /// <para>
    /// Both bounds are inclusive. On date members each bound is normalized independently, so
    /// <c>now-30d,now</c> is a window that moves with time exactly as the single-value operators do.
    /// </para>
    /// </remarks>
    /// <param name="name">Operator name as written in a rule.</param>
    /// <param name="negate">Whether the result is inverted.</param>
    public sealed class BetweenOperator(string name, bool negate) : RuleOperator
    {
        private static readonly MemberKind[] _appliesTo = [MemberKind.Number, MemberKind.Date];

        /// <inheritdoc />
        public override string Name { get; } = name;

        /// <inheritdoc />
        public override IReadOnlyList<MemberKind> AppliesTo => _appliesTo;

        /// <inheritdoc />
        public override ValueArity Arity => ValueArity.Pair;

        /// <inheritdoc />
        public override string? Notes { get; } = negate
            ? "Two values, low and high, separated by a comma. Holds when the value is outside that range. Both bounds are inclusive, so a value equal to either bound does not match."
            : "Two values, low and high, separated by a comma, such as 1980,1989. Both bounds are inclusive. On a date member each bound accepts the same forms a single date does, including offsets such as now-30d.";

        /// <inheritdoc />
        public override LinqExpression Build(RuleOperatorContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var parts = RuleValueList.Split(context.TargetValue);

            if (parts.Count != 2)
            {
                throw new ArgumentException(
                    $"Operator '{Name}' needs two values separated by a comma, but got '{context.TargetValue}'.",
                    nameof(context));
            }

            var lower = LinqExpression.GreaterThanOrEqual(context.Member, Constant(parts[0], context.MemberType));
            var upper = LinqExpression.LessThanOrEqual(context.Member, Constant(parts[1], context.MemberType));
            var range = LinqExpression.AndAlso(lower, upper);

            return negate ? LinqExpression.Not(range) : range;
        }

        /// <inheritdoc />
        public override RuleValueProblem? ValidateValue(string targetValue, MemberKind kind)
        {
            var parts = RuleValueList.Split(targetValue);

            if (parts.Count != 2)
            {
                return new RuleValueProblem(
                    "E15",
                    $"{Name} needs two values separated by a comma, such as 1980,1989. Got {parts.Count}.");
            }

            // Only numbers can be compared here. Date bounds arrive as readable dates or offsets and are
            // rewritten to Unix seconds later, so ordering them at this point would reject valid input.
            if (kind == MemberKind.Number
                && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var low)
                && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var high)
                && low > high)
            {
                return new RuleValueProblem(
                    "E16",
                    $"{Name} was given a low bound of {parts[0]} above its high bound of {parts[1]}, so it matches nothing. Swap them.");
            }

            return null;
        }
    }
}
