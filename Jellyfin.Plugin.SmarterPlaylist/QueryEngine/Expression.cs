namespace Jellyfin.Plugin.SmarterPlaylist.QueryEngine
{
    /// <summary>
    /// A single rule comparing one <see cref="Operand"/> property against a target value.
    /// </summary>
    /// <param name="memberName">Name of the <see cref="Operand"/> property to test.</param>
    /// <param name="operator">Comparison to apply, e.g. <c>Equal</c>, <c>Contains</c>, or <c>MatchRegex</c>.</param>
    /// <param name="targetValue">Value the property is compared against, as written in the playlist JSON.</param>
    public class Expression(string memberName, string @operator, string targetValue)
    {
        /// <summary>
        /// Gets or sets the name of the <see cref="Operand"/> property to test.
        /// </summary>
        public string MemberName { get; set; } = memberName;

        /// <summary>
        /// Gets or sets the comparison to apply.
        /// </summary>
        /// <remarks>
        /// Accepts any <see cref="System.Linq.Expressions.ExpressionType"/> name valid for the property type
        /// (such as <c>Equal</c> or <c>GreaterThan</c>), a method on the property type (such as <c>Contains</c>
        /// or <c>StartsWith</c>), or the pseudo-operators <c>MatchRegex</c> and <c>NotMatchRegex</c>.
        /// </remarks>
        public string Operator { get; set; } = @operator;

        /// <summary>
        /// Gets or sets the value the property is compared against.
        /// </summary>
        /// <remarks>
        /// Always written as a string in JSON and converted to the property's type when the rule is compiled.
        /// Date values are rewritten to Unix seconds by <see cref="Engine.FixRules"/> before compilation.
        /// </remarks>
        public string TargetValue { get; set; } = targetValue;
    }
}
