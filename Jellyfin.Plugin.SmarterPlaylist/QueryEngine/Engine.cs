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
        /// Lowest numeric value treated as a bare year rather than a Unix timestamp.
        /// </summary>
        private const double BareYearLowerBound = 1000;

        /// <summary>
        /// Highest numeric value treated as a bare year rather than a Unix timestamp.
        /// </summary>
        /// <remarks>
        /// As a genuine timestamp this range spans 1970-01-01T00:16:40Z to 1970-01-01T02:46:39Z,
        /// which nobody filters on, so treating it as a mistyped year costs nothing real.
        /// </remarks>
        private const double BareYearUpperBound = 9999;

        /// <summary>
        /// Matches a date written relative to the present, such as <c>now</c> or <c>now-30d</c>.
        /// </summary>
        private static readonly Regex _relativeDate = new(
            @"^\s*now\s*(?:([+-])\s*(\d{1,6})\s*([hdwmyHDWMY]))?\s*$",
            RegexOptions.IgnoreCase,
            TimeSpan.FromSeconds(1));

        /// <summary>
        /// Gets the members held as Unix seconds, whose rule values may be written as readable dates.
        /// </summary>
        /// <remarks>
        /// Public because the configuration page's schema must classify exactly these members as dates.
        /// Exposing the engine's own list, rather than keeping a parallel copy elsewhere, means the two
        /// cannot drift into disagreeing about which members accept a readable date.
        /// </remarks>
        public static IReadOnlyList<string> DateMembers { get; } =
        [
            nameof(Operand.PremiereDate),
            nameof(Operand.DateCreated),
            nameof(Operand.DateLastRefreshed),
            nameof(Operand.DateLastSaved),
            nameof(Operand.DateModified),
            nameof(Operand.LastPlayedDate)
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
        /// Produces a compile-ready copy of every supplied rule set.
        /// </summary>
        /// <remarks>
        /// This deliberately does not mutate <paramref name="rulesets"/>. The caller's rule sets belong
        /// to the deserialized <see cref="SmarterPlaylistDto"/>, which is written back to disk when a
        /// playlist is first created; normalizing in place would rewrite the user's readable dates as
        /// opaque Unix timestamps in their own file.
        /// </remarks>
        /// <param name="rulesets">Rule sets to normalize.</param>
        /// <returns>A new collection of normalized rule sets, leaving the input untouched.</returns>
        public static Collection<ExpressionSet> NormalizeRuleSets(Collection<ExpressionSet> rulesets)
        {
            ArgumentNullException.ThrowIfNull(rulesets);

            var normalized = new Collection<ExpressionSet>();

            foreach (var rules in rulesets)
            {
                normalized.Add(NormalizeRules(rules));
            }

            return normalized;
        }

        /// <summary>
        /// Produces a compile-ready copy of a rule set, with date values expressed as Unix seconds.
        /// </summary>
        /// <remarks>
        /// Date members are stored as Unix seconds, but playlist JSON is written with readable dates.
        /// This converts the latter to the former before the rule is compiled. Values that are already
        /// numeric are passed through, so definitions written against earlier versions keep working.
        /// </remarks>
        /// <param name="rules">Rule set to normalize.</param>
        /// <returns>A new rule set, leaving <paramref name="rules"/> untouched.</returns>
        /// <exception cref="ArgumentException">A date member's value is neither a date nor a Unix timestamp.</exception>
        public static ExpressionSet NormalizeRules(ExpressionSet rules)
        {
            ArgumentNullException.ThrowIfNull(rules);

            var normalized = new ExpressionSet();

            foreach (var rule in rules.Expressions)
            {
                normalized.Expressions.Add(
                    new Expression(rule.MemberName, rule.Operator, NormalizeTargetValue(rule)));
            }

            return normalized;
        }

        /// <summary>
        /// Returns a rule's target value in the form the compiled expression expects.
        /// </summary>
        /// <param name="rule">Rule whose value is being normalized.</param>
        /// <returns>The normalized value, or the original for members needing no conversion.</returns>
        /// <exception cref="ArgumentException">A date member's value is neither a date nor a Unix timestamp.</exception>
        private static string NormalizeTargetValue(Expression rule)
        {
            if (!DateMembers.Contains(rule.MemberName, StringComparer.Ordinal))
            {
                return rule.TargetValue;
            }

            // Resolved here rather than when the definition is saved, because normalization runs on
            // every refresh. That is what makes "not played in the last 30 days" a window that moves
            // with time instead of a fixed date that quietly goes stale.
            if (TryResolveRelative(rule.TargetValue, out var relative))
            {
                return ConvertToUnixTimestamp(relative).ToString(CultureInfo.InvariantCulture);
            }

            if (DateTime.TryParse(rule.TargetValue, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
            {
                return ConvertToUnixTimestamp(parsed).ToString(CultureInfo.InvariantCulture);
            }

            if (double.TryParse(rule.TargetValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric))
            {
                // A bare year does not parse as a date, so it would otherwise fall through as a raw
                // timestamp: "2020" would silently mean 33 minutes after the Unix epoch rather than
                // the year 2020, matching almost everything. Reject it rather than guess.
                if (numeric >= BareYearLowerBound && numeric <= BareYearUpperBound)
                {
                    throw new ArgumentException(
                        $"Value '{rule.TargetValue}' for date member '{rule.MemberName}' is ambiguous. Write a full date such as '{rule.TargetValue}-01-01' instead of a bare year.",
                        nameof(rule));
                }

                return rule.TargetValue;
            }

            throw new ArgumentException(
                $"Value '{rule.TargetValue}' for date member '{rule.MemberName}' is neither a date, a Unix timestamp, nor an offset from now such as 'now-30d'",
                nameof(rule));
        }

        /// <summary>
        /// Resolves a value expressed relative to the present, such as <c>now-30d</c>.
        /// </summary>
        /// <remarks>
        /// Accepts <c>now</c> on its own, or <c>now</c> followed by a signed offset and a unit:
        /// <c>h</c> hours, <c>d</c> days, <c>w</c> weeks, <c>m</c> months, <c>y</c> years. Months and
        /// years use calendar arithmetic, so <c>now-1m</c> means the same day last month rather than
        /// thirty days ago.
        /// </remarks>
        /// <param name="value">Value to interpret.</param>
        /// <param name="resolved">The resulting UTC instant, when the value is relative.</param>
        /// <returns><c>true</c> when the value was a relative expression.</returns>
        private static bool TryResolveRelative(string value, out DateTime resolved)
        {
            resolved = default;

            var match = _relativeDate.Match(value ?? string.Empty);
            if (!match.Success)
            {
                return false;
            }

            var now = DateTime.UtcNow;
            if (!match.Groups[1].Success)
            {
                resolved = now;

                return true;
            }

            var amount = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            if (match.Groups[1].Value == "-")
            {
                amount = -amount;
            }

            resolved = match.Groups[3].Value.ToLowerInvariant() switch
            {
                "h" => now.AddHours(amount),
                "d" => now.AddDays(amount),
                "w" => now.AddDays(amount * 7),
                "m" => now.AddMonths(amount),
                _ => now.AddYears(amount)
            };

            return true;
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
