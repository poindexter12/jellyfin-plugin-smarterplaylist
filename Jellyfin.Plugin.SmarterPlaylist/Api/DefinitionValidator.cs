using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jellyfin.Plugin.SmarterPlaylist.QueryEngine;
using Jellyfin.Plugin.SmarterPlaylist.QueryEngine.Operators;

namespace Jellyfin.Plugin.SmarterPlaylist.Api
{
    /// <summary>
    /// Checks a playlist definition for problems without running a refresh.
    /// </summary>
    /// <remarks>
    /// Every error here previously surfaced only as an exception mid-refresh, in the server log, after
    /// aborting the run. Validating on demand is what turns those into something a user can see and fix.
    /// </remarks>
    public static class DefinitionValidator
    {
        /// <summary>
        /// Validates a definition.
        /// </summary>
        /// <param name="dto">Definition to check.</param>
        /// <param name="schema">Filter vocabulary to check member and operator names against.</param>
        /// <returns>Every problem found, errors first.</returns>
        public static IReadOnlyList<Diagnostic> Validate(SmarterPlaylistDto dto, SchemaResponse schema)
        {
            ArgumentNullException.ThrowIfNull(dto);
            ArgumentNullException.ThrowIfNull(schema);

            var diagnostics = new List<Diagnostic>();

            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                diagnostics.Add(new Diagnostic("E01", DiagnosticSeverity.Error, "Name is required; it is the playlist's name in Jellyfin.", "Name"));
            }

            if (string.IsNullOrWhiteSpace(dto.User))
            {
                diagnostics.Add(new Diagnostic("E02", DiagnosticSeverity.Error, "User is required; it decides whose library the rules run against.", "User"));
            }

            if (dto.MaxItems < 0)
            {
                diagnostics.Add(new Diagnostic("E03", DiagnosticSeverity.Error, "MaxItems cannot be negative. Use 0 for the default.", "MaxItems"));
            }

            if (!schema.Orders.Contains(dto.Order.Name, StringComparer.Ordinal))
            {
                diagnostics.Add(new Diagnostic(
                    "W01",
                    DiagnosticSeverity.Warning,
                    $"Order '{dto.Order.Name}' is not recognised, so items will be left in library order. Valid values: {string.Join(", ", schema.Orders)}.",
                    "Order.Name"));
            }

            // Zero sets matches nothing; a set with zero rules matches everything. Both are almost
            // certainly a mistake, and neither produces an error at refresh time.
            if (dto.ExpressionSets.Count == 0)
            {
                diagnostics.Add(new Diagnostic("E04", DiagnosticSeverity.Error, "This definition has no rule groups, so it would match nothing.", "ExpressionSets"));
            }

            for (var s = 0; s < dto.ExpressionSets.Count; s++)
            {
                var set = dto.ExpressionSets[s];
                var setPath = $"ExpressionSets[{s}]";

                if (set.Expressions.Count == 0)
                {
                    diagnostics.Add(new Diagnostic("E05", DiagnosticSeverity.Error, "This rule group is empty, so it would match every item in the library.", setPath));
                    continue;
                }

                for (var e = 0; e < set.Expressions.Count; e++)
                {
                    ValidateExpression(set.Expressions[e], schema, $"{setPath}.Expressions[{e}]", diagnostics);
                }
            }

            return [.. diagnostics.OrderBy(d => d.Severity)];
        }

        /// <summary>
        /// Validates a single rule against the schema.
        /// </summary>
        /// <param name="rule">Rule to check.</param>
        /// <param name="schema">Filter vocabulary.</param>
        /// <param name="path">Location of the rule, for the diagnostic.</param>
        /// <param name="diagnostics">Collection to append problems to.</param>
        private static void ValidateExpression(Expression rule, SchemaResponse schema, string path, List<Diagnostic> diagnostics)
        {
            var member = schema.Members.FirstOrDefault(m => string.Equals(m.Name, rule.MemberName, StringComparison.Ordinal));

            if (member is null)
            {
                var caseInsensitive = schema.Members.FirstOrDefault(m => string.Equals(m.Name, rule.MemberName, StringComparison.OrdinalIgnoreCase));
                var hint = caseInsensitive is not null
                    ? $" Did you mean '{caseInsensitive.Name}'? Member names are case-sensitive."
                    : $" Valid members: {string.Join(", ", schema.Members.Where(m => m.Kind != MemberKind.Unsupported).Select(m => m.Name))}.";

                diagnostics.Add(new Diagnostic("E06", DiagnosticSeverity.Error, $"'{rule.MemberName}' is not a filterable property.{hint}", path));

                return;
            }

            if (member.Kind == MemberKind.Unsupported)
            {
                diagnostics.Add(new Diagnostic("E07", DiagnosticSeverity.Error, $"'{member.Name}' cannot be filtered on.", path));

                return;
            }

            if (!member.Operators.Contains(rule.Operator, StringComparer.Ordinal))
            {
                diagnostics.Add(new Diagnostic(
                    "E08",
                    DiagnosticSeverity.Error,
                    $"'{rule.Operator}' is not valid for {member.Name}. Valid operators: {string.Join(", ", member.Operators)}.",
                    path));

                return;
            }

            ValidateTargetValue(rule, member, path, diagnostics);
        }

        /// <summary>
        /// Checks that a rule's target value can be converted to the member's type.
        /// </summary>
        /// <param name="rule">Rule to check.</param>
        /// <param name="member">Member the rule targets.</param>
        /// <param name="path">Location of the rule, for the diagnostic.</param>
        /// <param name="diagnostics">Collection to append problems to.</param>
        private static void ValidateTargetValue(Expression rule, MemberDescriptor member, string path, List<Diagnostic> diagnostics)
        {
            var op = OperatorRegistry.Find(rule.Operator, member.Kind);

            // The operator speaks for its own value: whether a regex parses, whether a range has two
            // bounds, whether a list has a stray comma. Keeping those checks here meant the validator
            // held a second, always-incomplete copy of what each operator accepts.
            if (op is not null)
            {
                var problem = op.ValidateValue(rule.TargetValue, member.Kind);

                if (problem is not null)
                {
                    diagnostics.Add(new Diagnostic(problem.Code, DiagnosticSeverity.Error, problem.Message, path));

                    return;
                }

                // An operator taking no value has nothing left to check.
                if (op.Arity == ValueArity.None)
                {
                    return;
                }
            }

            // A multi-value operator holds several values of the member's type in one string, so each
            // one is checked separately. Checking the raw string would reject every well-formed range.
            var values = op is null || op.Arity == ValueArity.Single
                ? [rule.TargetValue]
                : RuleValueList.Split(rule.TargetValue);

            foreach (var value in values)
            {
                ValidateOneValue(value, member.Kind, path, diagnostics);
            }
        }

        /// <summary>
        /// Checks one value against the member's type.
        /// </summary>
        /// <param name="value">Value to check.</param>
        /// <param name="kind">Kind of the member the rule targets.</param>
        /// <param name="path">Location of the rule, for the diagnostic.</param>
        /// <param name="diagnostics">Collection to append problems to.</param>
        private static void ValidateOneValue(string value, MemberKind kind, string path, List<Diagnostic> diagnostics)
        {
            switch (kind)
            {
                case MemberKind.Date:
                    // Mirrors the engine: a readable date, a raw timestamp, or an offset from now, but
                    // never a bare year -- "2020" would otherwise be read as 33 minutes after the epoch.
                    if (Engine.IsRelativeDate(value)
                        || DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out _))
                    {
                        break;
                    }

                    if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric))
                    {
                        diagnostics.Add(new Diagnostic("E09", DiagnosticSeverity.Error, $"'{value}' is neither a date nor a Unix timestamp.", path));
                    }
                    else if (numeric is >= 1000 and <= 9999)
                    {
                        diagnostics.Add(new Diagnostic(
                            "E10",
                            DiagnosticSeverity.Error,
                            $"'{value}' is ambiguous: as a timestamp it means {SchemaBuilder.FormatUnixSeconds(numeric)}, not the year {value}. Write a full date such as {value}-01-01.",
                            path));
                    }

                    break;

                case MemberKind.Boolean:
                    if (!bool.TryParse(value, out _))
                    {
                        diagnostics.Add(new Diagnostic("E11", DiagnosticSeverity.Error, $"'{value}' is not True or False.", path));
                    }

                    break;

                case MemberKind.Number:
                    if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    {
                        diagnostics.Add(new Diagnostic("E12", DiagnosticSeverity.Error, $"'{value}' is not a number.", path));
                    }

                    break;

                default:
                    break;
            }
        }
    }
}
