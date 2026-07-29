namespace Jellyfin.Plugin.SmarterPlaylist.Api
{
    /// <summary>
    /// One operator, as the UI needs to render an input for it.
    /// </summary>
    /// <remarks>
    /// The page cannot choose a control from the member's kind alone. A number member takes a numeric
    /// input for <c>GreaterThan</c> and a free-text one for <c>Between</c>, whose value is two numbers
    /// and a comma; <c>IsEmpty</c> takes no input at all. <see cref="Arity"/> is what tells it which.
    /// </remarks>
    /// <param name="Name">Operator name, matching a rule's <c>Operator</c> field.</param>
    /// <param name="Arity">How many values the operator reads from the rule's target value.</param>
    /// <param name="Notes">Behaviour a user would otherwise get wrong, or <c>null</c>.</param>
    public sealed record OperatorDescriptor(string Name, string Arity, string? Notes);
}
