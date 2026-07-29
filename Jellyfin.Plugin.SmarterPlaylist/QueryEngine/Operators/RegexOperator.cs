using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using LinqExpression = System.Linq.Expressions.Expression;

namespace Jellyfin.Plugin.SmarterPlaylist.QueryEngine.Operators
{
    /// <summary>
    /// Tests a member against a regular expression.
    /// </summary>
    /// <remarks>
    /// List members are matched element-wise, so the rule holds when any single element matches.
    /// Matching the collection's own <see cref="object.ToString"/> would test the CLR type name
    /// instead of its contents, which silently never matches.
    /// </remarks>
    /// <param name="name">Operator name as written in a rule.</param>
    /// <param name="negate">Whether the result is inverted.</param>
    public sealed class RegexOperator(string name, bool negate) : RuleOperator
    {
        /// <summary>
        /// How long a single match may run before it is abandoned.
        /// </summary>
        private static readonly TimeSpan _matchTimeout = TimeSpan.FromSeconds(5);

        private static readonly MemberKind[] _appliesTo = [MemberKind.Text, MemberKind.TextEnum, MemberKind.TextList];

        /// <inheritdoc />
        public override string Name { get; } = name;

        /// <inheritdoc />
        public override IReadOnlyList<MemberKind> AppliesTo => _appliesTo;

        /// <inheritdoc />
        public override string? Notes { get; } = negate
            ? "Holds only when no element matches."
            : "Tests each element of a list member, and the whole value of a text member.";

        /// <inheritdoc />
        public override LinqExpression Build(RuleOperatorContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var regex = LinqExpression.Constant(new Regex(context.TargetValue, RegexOptions.None, _matchTimeout));
            var isMatch = typeof(Regex).GetMethod(nameof(Regex.IsMatch), [typeof(string)])
                ?? throw new MissingMethodException(nameof(Regex), nameof(Regex.IsMatch));

            LinqExpression call;

            if (context.Kind == MemberKind.TextList)
            {
                call = AnyElement(context.Member, element => LinqExpression.Call(regex, isMatch, element));
            }
            else
            {
                var toString = context.MemberType.GetMethod(nameof(ToString), Type.EmptyTypes)
                    ?? throw new MissingMethodException(context.MemberType.Name, nameof(ToString));

                call = LinqExpression.Call(regex, isMatch, LinqExpression.Call(context.Member, toString));
            }

            return negate ? LinqExpression.Not(call) : call;
        }

        /// <inheritdoc />
        public override RuleValueProblem? ValidateValue(string targetValue, MemberKind kind)
        {
            try
            {
                _ = Regex.Match(string.Empty, targetValue, RegexOptions.None, TimeSpan.FromSeconds(1));
            }
            catch (ArgumentException ex)
            {
                return new RuleValueProblem("E14", $"'{targetValue}' is not a valid regular expression: {ex.Message}");
            }

            return null;
        }
    }
}
