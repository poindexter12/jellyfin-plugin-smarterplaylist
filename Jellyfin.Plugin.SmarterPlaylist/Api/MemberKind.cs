namespace Jellyfin.Plugin.SmarterPlaylist.Api
{
    /// <summary>
    /// How a filterable member should be presented and which operators apply to it.
    /// </summary>
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
