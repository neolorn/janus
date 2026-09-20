using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// What a browser is asked for when a credential is created.
/// </summary>
/// <param name="RelyingPartyId">What the credential is bound to.</param>
/// <param name="Algorithms">The COSE algorithms, in preference order.</param>
/// <param name="DiscoverableCredential">
/// Whether the authenticator keeps the credential and can offer it unprompted: a
/// passkey does, a second-factor security key does not (AUTH-FACT-002b).
/// </param>
/// <param name="Challenge">The value the authenticator signs over.</param>
/// <remarks>
/// Implements AUTH-FACT-002b, AUTH-FACT-014 and AUTH-STEP-007. User verification is
/// required of every ceremony, so there is nothing here that could ask for less.
/// </remarks>
public sealed record CredentialCeremony(
    string RelyingPartyId,
    IReadOnlyList<int> Algorithms,
    bool DiscoverableCredential,
    string Challenge);
