using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Jellyfin.Plugin.SmarterPlaylist.QueryEngine
{
    /// <summary>
    /// Decides which <see cref="MemberKind"/> an <see cref="Operand"/> property belongs to.
    /// </summary>
    /// <remarks>
    /// Both the engine and the configuration page's schema need this answer, and they must never
    /// disagree: the page offers the operators a kind allows, and the engine accepts the operators a
    /// kind allows. Two copies of the rules would let a member drift into being offered one vocabulary
    /// and evaluated against another.
    /// </remarks>
    public static class MemberClassifier
    {
        private static readonly Type[] _numericTypes =
        [
            typeof(float), typeof(double), typeof(int), typeof(long), typeof(short), typeof(decimal)
        ];

        /// <summary>
        /// Classifies a property.
        /// </summary>
        /// <param name="property">Property to classify.</param>
        /// <returns>The kind that decides its operators and its UI control.</returns>
        public static MemberKind Classify(PropertyInfo property)
        {
            ArgumentNullException.ThrowIfNull(property);

            return Classify(property.Name, property.PropertyType);
        }

        /// <summary>
        /// Classifies a member by name and CLR type.
        /// </summary>
        /// <param name="name">Property name, which decides the date and enum special cases.</param>
        /// <param name="type">Declared CLR type of the property.</param>
        /// <returns>The kind that decides its operators and its UI control.</returns>
        public static MemberKind Classify(string name, Type type)
        {
            ArgumentNullException.ThrowIfNull(type);

            var underlying = Nullable.GetUnderlyingType(type) ?? type;

            // Checked in order; the first match wins. The date set is explicit rather than a name-suffix
            // heuristic, which would misclassify DateCreated and its siblings as plain numbers.
            if (Engine.DateMembers.Contains(name, StringComparer.Ordinal))
            {
                return MemberKind.Date;
            }

            if (underlying == typeof(bool))
            {
                return MemberKind.Boolean;
            }

            if (type == typeof(string))
            {
                return string.Equals(name, nameof(Operand.MediaType), StringComparison.Ordinal)
                    ? MemberKind.TextEnum
                    : MemberKind.Text;
            }

            if (typeof(IEnumerable<string>).IsAssignableFrom(type))
            {
                return MemberKind.TextList;
            }

            if (Array.IndexOf(_numericTypes, underlying) >= 0)
            {
                return MemberKind.Number;
            }

            // Terminal fallback. A member landing here renders as unsupported rather than silently
            // offering operators that would throw at refresh time.
            return MemberKind.Unsupported;
        }
    }
}
