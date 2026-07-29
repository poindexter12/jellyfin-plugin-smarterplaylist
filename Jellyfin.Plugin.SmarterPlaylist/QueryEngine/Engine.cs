using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.SmarterPlaylist.QueryEngine.Operators;

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

            // Multi-value operators hold several dates in one string, so each bound is rewritten on its
            // own. Normalizing the whole string would leave "now-30d,now" unparseable as a single date
            // and reject a range that is perfectly well formed.
            var op = OperatorRegistry.Find(rule.Operator, MemberKind.Date);

            switch (op?.Arity)
            {
                case ValueArity.None:
                    return rule.TargetValue;

                case ValueArity.Pair:
                case ValueArity.List:
                    return RuleValueList.Join(
                        [.. RuleValueList.Split(rule.TargetValue).Select(part => NormalizeDate(part, rule.MemberName))]);

                case null:
                    // An unrecognised operator; leave the value alone so the failure reported is the
                    // operator, which is the real problem, rather than a date that never had to parse.
                    return rule.TargetValue;

                default:
                    return NormalizeDate(rule.TargetValue, rule.MemberName);
            }
        }

        /// <summary>
        /// Rewrites one date value as Unix seconds.
        /// </summary>
        /// <param name="value">Value as written in the rule.</param>
        /// <param name="memberName">Member the value belongs to, for the error message.</param>
        /// <returns>The value in Unix seconds.</returns>
        /// <exception cref="ArgumentException">The value is neither a date nor a Unix timestamp.</exception>
        private static string NormalizeDate(string value, string memberName)
        {
            // Resolved here rather than when the definition is saved, because normalization runs on
            // every refresh. That is what makes "not played in the last 30 days" a window that moves
            // with time instead of a fixed date that quietly goes stale.
            if (TryResolveRelative(value, out var relative))
            {
                return ConvertToUnixTimestamp(relative).ToString(CultureInfo.InvariantCulture);
            }

            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
            {
                return ConvertToUnixTimestamp(parsed).ToString(CultureInfo.InvariantCulture);
            }

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric))
            {
                // A bare year does not parse as a date, so it would otherwise fall through as a raw
                // timestamp: "2020" would silently mean 33 minutes after the Unix epoch rather than
                // the year 2020, matching almost everything. Reject it rather than guess.
                if (numeric >= BareYearLowerBound && numeric <= BareYearUpperBound)
                {
                    throw new ArgumentException(
                        $"Value '{value}' for date member '{memberName}' is ambiguous. Write a full date such as '{value}-01-01' instead of a bare year.",
                        nameof(value));
                }

                return value;
            }

            throw new ArgumentException(
                $"Value '{value}' for date member '{memberName}' is neither a date, a Unix timestamp, nor an offset from now such as 'now-30d'",
                nameof(value));
        }

        /// <summary>
        /// Reports whether a value is written relative to the present, such as <c>now-30d</c>.
        /// </summary>
        /// <remarks>
        /// Public so validation can recognise the same forms the engine resolves. Without it, the
        /// validator has only <see cref="DateTime.TryParse(string, IFormatProvider, DateTimeStyles, out DateTime)"/>
        /// to go on, which rejects every offset -- so the configuration page's own relative date mode
        /// produces values its own validator calls errors.
        /// </remarks>
        /// <param name="value">Value to inspect.</param>
        /// <returns><c>true</c> when the value is an offset from now.</returns>
        public static bool IsRelativeDate(string value) => _relativeDate.IsMatch(value ?? string.Empty);

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
                // 7.0, not 7: multiplying the int first computes the whole product in int range
                // before widening, which is the pattern that overflows for large operands even though
                // the six-digit cap on amount keeps this particular one safe.
                "w" => now.AddDays(amount * 7.0),
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
            var property = typeof(T).GetProperty(r.MemberName)
                ?? throw new ArgumentException($"Unknown member '{r.MemberName}' on type '{typeof(T).Name}'", nameof(r));

            var kind = MemberClassifier.Classify(property);
            var op = OperatorRegistry.Find(r.Operator, kind)
                ?? throw new ArgumentException(
                    $"Operator '{r.Operator}' is not valid for member '{r.MemberName}'. Valid operators: {string.Join(", ", OperatorRegistry.NamesForKind(kind))}",
                    nameof(r));

            var left = System.Linq.Expressions.Expression.Property(param, property);

            return op.Build(new RuleOperatorContext(left, property.PropertyType, kind, r.TargetValue));
        }
    }
}
