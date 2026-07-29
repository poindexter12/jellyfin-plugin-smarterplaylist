using System.Collections.Generic;
using Jellyfin.Plugin.SmarterPlaylist.QueryEngine;

namespace Jellyfin.Plugin.SmarterPlaylist.Api
{
    /// <summary>
    /// One filterable member of <see cref="QueryEngine.Operand"/>, as the UI needs to render it.
    /// </summary>
    /// <param name="Name">Property name, which is what a rule's <c>MemberName</c> must contain.</param>
    /// <param name="ClrType">Underlying CLR type, for diagnostics.</param>
    /// <param name="Kind">How the UI should present it.</param>
    /// <param name="Operators">Operators valid for this member.</param>
    /// <param name="DateRewritten">Whether readable dates are converted to Unix seconds for this member.</param>
    /// <param name="Notes">Behaviour a user would otherwise get wrong.</param>
    /// <param name="Minimum">Lowest sensible value, for a numeric control. Advisory, not enforced.</param>
    /// <param name="Maximum">Highest sensible value, for a numeric control. Advisory, not enforced.</param>
    /// <param name="Step">Granularity for a numeric control.</param>
    /// <param name="LibraryValues">
    /// Whether the values this member takes can be listed from the library, so the UI can offer them
    /// rather than asking the user to type a name it will only compare exactly.
    /// </param>
    public sealed record MemberDescriptor(
        string Name,
        string ClrType,
        MemberKind Kind,
        IReadOnlyList<string> Operators,
        bool DateRewritten,
        string? Notes,
        double? Minimum = null,
        double? Maximum = null,
        double? Step = null,
        bool LibraryValues = false);
}
