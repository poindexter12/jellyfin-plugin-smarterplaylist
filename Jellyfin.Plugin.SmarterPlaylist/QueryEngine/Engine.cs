using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.SmarterPlaylist.QueryEngine
{
    /// <summary>
    /// Compiles playlist rules into delegates that can be evaluated against an <see cref="Operand"/>.
    /// </summary>
    /// <remarks>
    /// The rule-to-expression-tree approach is adapted from
    /// <see href="https://stackoverflow.com/questions/6488034/how-to-implement-a-rule-engine">this Stack Overflow answer</see>.
    /// </remarks>
    public static class Engine
    {
        /// <summary>
        /// Name of the pseudo-operator that tests a property against a regular expression.
        /// </summary>
        private const string MatchRegexOperator = "MatchRegex";

        /// <summary>
        /// Name of the pseudo-operator that negates a regular expression test.
        /// </summary>
        private const string NotMatchRegexOperator = "NotMatchRegex";

        /// <summary>
        /// Members held as Unix seconds, whose rule values may be written as readable dates.
        /// </summary>
        private static readonly string[] _dateMembers =
        [
            nameof(Operand.PremiereDate),
            nameof(Operand.DateCreated),
            nameof(Operand.DateLastRefreshed),
            nameof(Operand.DateLastSaved),
            nameof(Operand.DateModified)
        ];

        /// <summary>
        /// Compiles a single rule into a predicate over <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Type the rule is evaluated against, normally <see cref="Operand"/>.</typeparam>
        /// <param name="r">Rule to compile.</param>
        /// <returns>A predicate returning <c>true</c> when the rule matches.</returns>
        /// <exception cref="ArgumentException">The rule names a property that does not exist on <typeparamref name="T"/>.</exception>
        /// <exception cref="MissingMethodException">The rule names an operator the property type does not support.</exception>
        public static Func<T, bool> CompileRule<T>(Expression r)
        {
            var param = System.Linq.Expressions.Expression.Parameter(typeof(T));
            var expr = BuildExpr<T>(r, param);

            return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(expr, param).Compile(true);
        }

        /// <summary>
        /// Normalizes every rule in each supplied set so the rules are ready to compile.
        /// </summary>
        /// <param name="rulesets">Rule sets to normalize in place.</param>
        /// <returns>The same <paramref name="rulesets"/> instance, for chaining.</returns>
        public static Collection<ExpressionSet> FixRuleSets(Collection<ExpressionSet> rulesets)
        {
            ArgumentNullException.ThrowIfNull(rulesets);

            foreach (var rules in rulesets)
            {
                FixRules(rules);
            }

            return rulesets;
        }

        /// <summary>
        /// Rewrites date-valued rules in a set so their target values are Unix seconds.
        /// </summary>
        /// <remarks>
        /// Date members are stored as Unix seconds, but playlist JSON is written with readable dates.
        /// This converts the latter to the former before the rule is compiled. Values that are already
        /// numeric are left alone, so definitions written against earlier versions keep working.
        /// </remarks>
        /// <param name="rules">Rule set to normalize in place.</param>
        /// <returns>The same <paramref name="rules"/> instance, for chaining.</returns>
        /// <exception cref="ArgumentException">A date member's value is neither a date nor a Unix timestamp.</exception>
        public static ExpressionSet FixRules(ExpressionSet rules)
        {
            ArgumentNullException.ThrowIfNull(rules);

            foreach (var rule in rules.Expressions)
            {
                if (Array.IndexOf(_dateMembers, rule.MemberName) < 0)
                {
                    continue;
                }

                if (DateTime.TryParse(rule.TargetValue, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
                {
                    rule.TargetValue = ConvertToUnixTimestamp(parsed).ToString(CultureInfo.InvariantCulture);
                }
                else if (!double.TryParse(rule.TargetValue, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                {
                    throw new ArgumentException(
                        $"Value '{rule.TargetValue}' for date member '{rule.MemberName}' is neither a date nor a Unix timestamp",
                        nameof(rules));
                }
            }

            return rules;
        }

        /// <summary>
        /// Converts a date to whole seconds since the Unix epoch.
        /// </summary>
        /// <param name="date">Date to convert.</param>
        /// <returns>Seconds elapsed since 1970-01-01T00:00:00Z, rounded down.</returns>
        public static double ConvertToUnixTimestamp(DateTime date)
        {
            var origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            var diff = date.ToUniversalTime() - origin;

            return Math.Floor(diff.TotalSeconds);
        }

        /// <summary>
        /// Builds the expression tree for a single rule.
        /// </summary>
        /// <typeparam name="T">Type the rule is evaluated against.</typeparam>
        /// <param name="r">Rule to translate.</param>
        /// <param name="param">Parameter expression representing the instance under test.</param>
        /// <returns>A boolean-valued expression implementing the rule.</returns>
        private static System.Linq.Expressions.Expression BuildExpr<T>(Expression r, ParameterExpression param)
        {
            var left = System.Linq.Expressions.Expression.Property(param, r.MemberName);
            var tProp = typeof(T).GetProperty(r.MemberName)?.PropertyType
                ?? throw new ArgumentException($"Unknown member '{r.MemberName}' on type '{typeof(T).Name}'", nameof(r));

            // A rule whose operator names a built-in expression type becomes a binary comparison.
            if (Enum.TryParse(r.Operator, out ExpressionType tBinary))
            {
                var right = System.Linq.Expressions.Expression.Constant(Convert.ChangeType(r.TargetValue, tProp, CultureInfo.InvariantCulture));

                return System.Linq.Expressions.Expression.MakeBinary(tBinary, left, right);
            }

            if (r.Operator == MatchRegexOperator || r.Operator == NotMatchRegexOperator)
            {
                return BuildRegexExpr(r, left, tProp);
            }

            // Anything else is a method on the property type, e.g. 'Contains' -> 'operand.Genres.Contains("Comedy")'.
            var argumentTypes = tProp == typeof(string) ? new[] { typeof(string) } : null;
            var method = (argumentTypes is null ? tProp.GetMethod(r.Operator) : tProp.GetMethod(r.Operator, argumentTypes))
                ?? throw new MissingMethodException(tProp.Name, r.Operator);
            var tParam = method.GetParameters()[0].ParameterType;
            var methodArg = System.Linq.Expressions.Expression.Constant(Convert.ChangeType(r.TargetValue, tParam, CultureInfo.InvariantCulture));

            return System.Linq.Expressions.Expression.Call(left, method, methodArg);
        }

        /// <summary>
        /// Builds the expression tree for a regular-expression rule.
        /// </summary>
        /// <remarks>
        /// String-valued members are matched directly. Members holding a collection of strings are
        /// matched element-wise, so the rule holds when any single element matches. Matching the
        /// collection's own <see cref="object.ToString"/> would test the CLR type name instead of
        /// its contents, which silently never matches.
        /// </remarks>
        /// <param name="r">Rule to translate.</param>
        /// <param name="left">Expression yielding the property being tested.</param>
        /// <param name="tProp">Type of the property being tested.</param>
        /// <returns>A boolean-valued expression implementing the rule.</returns>
        private static System.Linq.Expressions.Expression BuildRegexExpr(Expression r, System.Linq.Expressions.Expression left, Type tProp)
        {
            var regex = new Regex(r.TargetValue, RegexOptions.None, TimeSpan.FromSeconds(5));
            var isMatch = typeof(Regex).GetMethod(nameof(Regex.IsMatch), new[] { typeof(string) })
                ?? throw new MissingMethodException(nameof(Regex), nameof(Regex.IsMatch));
            var regexInstance = System.Linq.Expressions.Expression.Constant(regex);

            System.Linq.Expressions.Expression call;
            if (typeof(IEnumerable<string>).IsAssignableFrom(tProp))
            {
                var element = System.Linq.Expressions.Expression.Parameter(typeof(string), "element");
                var predicate = System.Linq.Expressions.Expression.Lambda<Func<string, bool>>(
                    System.Linq.Expressions.Expression.Call(regexInstance, isMatch, element),
                    element);
                var any = typeof(Enumerable).GetMethods()
                    .Single(m => m.Name == nameof(Enumerable.Any) && m.GetParameters().Length == 2)
                    .MakeGenericMethod(typeof(string));

                call = System.Linq.Expressions.Expression.Call(any, left, predicate);
            }
            else
            {
                var toString = tProp.GetMethod(nameof(ToString), Type.EmptyTypes)
                    ?? throw new MissingMethodException(tProp.Name, nameof(ToString));

                call = System.Linq.Expressions.Expression.Call(
                    regexInstance,
                    isMatch,
                    System.Linq.Expressions.Expression.Call(left, toString));
            }

            return r.Operator == NotMatchRegexOperator
                ? System.Linq.Expressions.Expression.Not(call)
                : call;
        }
    }
}
