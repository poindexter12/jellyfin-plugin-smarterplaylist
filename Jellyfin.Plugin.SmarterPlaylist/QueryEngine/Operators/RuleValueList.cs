using System.Collections.Generic;
using System.Text;

namespace Jellyfin.Plugin.SmarterPlaylist.QueryEngine.Operators
{
    /// <summary>
    /// Reads and writes the comma-separated values that multi-value operators encode in a rule.
    /// </summary>
    /// <remarks>
    /// A comma inside a value is written <c>\,</c>. Without an escape, a genre or studio containing a
    /// comma would silently split into two values that match nothing, which is the kind of failure that
    /// looks like the filter is broken rather than like the value needs quoting.
    /// </remarks>
    public static class RuleValueList
    {
        /// <summary>
        /// Splits a target value into its parts, honouring <c>\,</c> as a literal comma.
        /// </summary>
        /// <param name="value">Raw target value from the rule.</param>
        /// <returns>The parts, each trimmed of surrounding whitespace.</returns>
        public static IReadOnlyList<string> Split(string? value)
        {
            var parts = new List<string>();

            if (string.IsNullOrEmpty(value))
            {
                parts.Add(string.Empty);

                return parts;
            }

            var current = new StringBuilder();

            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];

                if (c == '\\' && i + 1 < value.Length && value[i + 1] == ',')
                {
                    current.Append(',');
                    i++;

                    continue;
                }

                if (c == ',')
                {
                    parts.Add(current.ToString().Trim());
                    current.Clear();

                    continue;
                }

                current.Append(c);
            }

            parts.Add(current.ToString().Trim());

            return parts;
        }

        /// <summary>
        /// Joins parts back into a target value, escaping any commas they contain.
        /// </summary>
        /// <param name="parts">Parts to join.</param>
        /// <returns>A target value that <see cref="Split"/> turns back into <paramref name="parts"/>.</returns>
        public static string Join(IEnumerable<string> parts)
        {
            var builder = new StringBuilder();

            foreach (var part in parts)
            {
                if (builder.Length > 0)
                {
                    builder.Append(',');
                }

                builder.Append(part.Replace(",", "\\,", System.StringComparison.Ordinal));
            }

            return builder.ToString();
        }
    }
}
