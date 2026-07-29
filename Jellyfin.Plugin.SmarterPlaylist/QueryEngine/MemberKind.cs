namespace Jellyfin.Plugin.SmarterPlaylist.QueryEngine
{
    /// <summary>
    /// How a filterable member should be presented and which operators apply to it.
    /// </summary>
    /// <remarks>
    /// This lives in the query engine rather than the API layer because it is what
    /// <see cref="Operators.OperatorRegistry"/> keys operator applicability on. The configuration page
    /// also renders from it, and the member names below are serialized into the schema response, so
    /// they are a wire contract: renaming one changes the JSON the page switches on.
    /// </remarks>
    public enum MemberKind
    {
        /// <summary>Free text.</summary>
        Text,

        /// <summary>Text drawn from a fixed set of values.</summary>
        TextEnum,

        /// <summary>A collection of text values.</summary>
        TextList,

        /// <summary>A numeric value.</summary>
        Number,

        /// <summary>A date, stored as Unix seconds.</summary>
        Date,

        /// <summary>A true/false value.</summary>
        Boolean,

        /// <summary>A type this UI cannot render controls for.</summary>
        Unsupported
    }
}
