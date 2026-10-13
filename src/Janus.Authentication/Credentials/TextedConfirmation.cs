using System.Collections.Generic;
using Janus.Core;

namespace Janus.Authentication.Credentials;

/// <summary>
/// What became of a code presented to confirm a second step the library texts.
/// </summary>
/// <param name="Enrolled">The credential the code made active, where it was right.</param>
/// <param name="Retired">The credentials of the entry it retired.</param>
/// <param name="Refusal">Why it confirmed nothing, where it did not.</param>
/// <param name="Kept">
/// Whether a write stands that the caller commits: the credential, or a wrong try
/// counted against the code (CONV-DESIGN-003).
/// </param>
/// <remarks>Implements AUTH-FACT-002 and AUTH-FACT-004.</remarks>
internal sealed record TextedConfirmation(
    AuthenticatorId? Enrolled,
    IReadOnlyList<AuthenticatorId> Retired,
    Error? Refusal,
    bool Kept)
{
    /// <summary>
    /// A code that confirmed nothing.
    /// </summary>
    /// <param name="refusal">Why.</param>
    /// <param name="kept">Whether a write stands that the caller commits.</param>
    /// <returns>The outcome.</returns>
    public static TextedConfirmation Refused(Error refusal, bool kept) =>
        new(Enrolled: null, [], refusal, kept);
}
