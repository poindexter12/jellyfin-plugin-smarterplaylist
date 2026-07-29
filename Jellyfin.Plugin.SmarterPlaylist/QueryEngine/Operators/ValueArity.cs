namespace Jellyfin.Plugin.SmarterPlaylist.QueryEngine.Operators
{
    /// <summary>
    /// How many values an operator reads out of a rule's single <see cref="Expression.TargetValue"/>.
    /// </summary>
    /// <remarks>
    /// A rule carries exactly one string, because that is what playlist JSON has always held and what
    /// the configuration page renders one input for. Operators wanting more than one value encode them
    /// as a comma-separated list inside that string, and this says how many to expect. Normalization
    /// and validation both branch on it, so an operator declaring its arity gets date rewriting and
    /// "wrong number of values" checking without either of them naming the operator.
    /// </remarks>
    public enum ValueArity
    {
        /// <summary>Takes no value; whatever the rule carries is ignored.</summary>
        None,

        /// <summary>Takes exactly one value, the whole target string.</summary>
        Single,

        /// <summary>Takes exactly two comma-separated values, such as the bounds of a range.</summary>
        Pair,

        /// <summary>Takes one or more comma-separated values.</summary>
        List
    }
}
