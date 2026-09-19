using Janus.Core;

namespace Janus.Authentication.Sending;

/// <summary>
/// One attempt the progressive delay looks at, in the three terms it is counted
/// under.
/// </summary>
/// <param name="Source">The address it came from.</param>
/// <param name="Identifier">
/// What was typed, whether or not an account holds it.
/// </param>
/// <remarks>Implements AUTH-ABUSE-001 and AUTH-ABUSE-002.</remarks>
internal sealed record ThrottleAttempt(string Source, string? Identifier)
{
    /// <summary>
    /// The account it was made against, where the identifier resolved to one.
    /// </summary>
    public SubjectId? Account { get; init; }

    /// <summary>
    /// Whether the source arrived with a browser the account already knows, which
    /// exempts it from the components an attacker raises from anywhere.
    /// </summary>
    public bool Recognised { get; init; }
}
