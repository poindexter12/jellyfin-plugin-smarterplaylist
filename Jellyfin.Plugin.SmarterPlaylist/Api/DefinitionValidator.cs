using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jellyfin.Plugin.SmarterPlaylist.QueryEngine;

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
            switch (member.Kind)
            {
                case MemberKind.Date:
                    // Mirrors the engine: a readable date, or a raw timestamp, but never a bare year --
                    // "2020" would otherwise be read as 33 minutes after the Unix epoch.
                    if (!DateTime.TryParse(rule.TargetValue, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out _))
                    {
                        if (!double.TryParse(rule.TargetValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric))
                        {
                            diagnostics.Add(new Diagnostic("E09", DiagnosticSeverity.Error, $"'{rule.TargetValue}' is neither a date nor a Unix timestamp.", path));
                        }
                        else if (numeric is >= 1000 and <= 9999)
                        {
                            diagnostics.Add(new Diagnostic(
                                "E10",
                                DiagnosticSeverity.Error,
                                $"'{rule.TargetValue}' is ambiguous: as a timestamp it means {SchemaBuilder.FormatUnixSeconds(numeric)}, not the year {rule.TargetValue}. Write a full date such as {rule.TargetValue}-01-01.",
                                path));
                        }
                    }

                    break;

                case MemberKind.Boolean:
                    if (!bool.TryParse(rule.TargetValue, out _))
                    {
                        diagnostics.Add(new Diagnostic("E11", DiagnosticSeverity.Error, $"'{rule.TargetValue}' is not True or False.", path));
                    }

                    break;

                case MemberKind.Number:
                    if (!double.TryParse(rule.TargetValue, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    {
                        diagnostics.Add(new Diagnostic("E12", DiagnosticSeverity.Error, $"'{rule.TargetValue}' is not a number.", path));
                    }

                    break;

                case MemberKind.TextList:
                    if (rule.Operator == "Contains" && string.IsNullOrEmpty(rule.TargetValue))
                    {
                        diagnostics.Add(new Diagnostic("E13", DiagnosticSeverity.Error, "Contains needs a value to look for.", path));
                    }

                    break;

                default:
                    break;
            }

            if (rule.Operator is "MatchRegex" or "NotMatchRegex")
            {
                try
                {
                    _ = System.Text.RegularExpressions.Regex.Match(string.Empty, rule.TargetValue, System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1));
                }
                catch (ArgumentException ex)
                {
                    diagnostics.Add(new Diagnostic("E14", DiagnosticSeverity.Error, $"'{rule.TargetValue}' is not a valid regular expression: {ex.Message}", path));
                }
            }
        }
    }
}
