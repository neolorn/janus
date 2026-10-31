using System;
using System.Text.Json.Serialization;
using Janus.Core;

namespace Janus.Hosting.Authentication;

/// <summary>
/// What asking for a sign-in link answers, as the authentication application receives
/// it.
/// </summary>
/// <param name="ChallengeId">
/// The handle of the sign-in the ask opened, absent where the link went on one the
/// browser holds.
/// </param>
/// <remarks>Implements AUTH-FACT-003 AC6 and AUTH-ABUSE-002.</remarks>
internal sealed record RequestedLinkView(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ChallengeId)
{
    /// <summary>
    /// Reads an answered ask.
    /// </summary>
    /// <param name="requested">What the ask answered with.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The answer is absent.</exception>
    public static RequestedLinkView Of(RequestedLink requested)
    {
        ArgumentNullException.ThrowIfNull(requested);

        return new RequestedLinkView(requested.Challenge);
    }
}
