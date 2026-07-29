using System;
using LinqExpression = System.Linq.Expressions.Expression;

namespace Jellyfin.Plugin.SmarterPlaylist.QueryEngine.Operators
{
    /// <summary>
    /// The member being tested and the value to test it against.
    /// </summary>
    /// <param name="Member">Expression yielding the member's value from the item under test.</param>
    /// <param name="MemberType">Declared CLR type of the member.</param>
    /// <param name="Kind">Classification the operator was selected for.</param>
    /// <param name="TargetValue">Target value as written in the rule, after date normalization.</param>
    public sealed record RuleOperatorContext(
        LinqExpression Member,
        Type MemberType,
        MemberKind Kind,
        string TargetValue);
}
