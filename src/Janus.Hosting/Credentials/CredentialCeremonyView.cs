using System;
using System.Collections.Generic;
using Janus.Core;

namespace Janus.Hosting.Credentials;

/// <summary>
/// What the browser is asked for.
/// </summary>
/// <param name="RelyingPartyId">What the credential is bound to.</param>
/// <param name="Algorithms">The COSE algorithms, in preference order.</param>
/// <param name="DiscoverableCredential">Whether the authenticator keeps the credential.</param>
/// <param name="Challenge">The value the authenticator signs over.</param>
/// <remarks>Implements AUTH-FACT-002b and AUTH-FACT-014.</remarks>
internal sealed record CredentialCeremonyView(
    string RelyingPartyId,
    IReadOnlyList<int> Algorithms,
    bool DiscoverableCredential,
    string Challenge)
{
    /// <summary>
    /// Reads a ceremony.
    /// </summary>
    /// <param name="ceremony">The ceremony.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The ceremony is absent.</exception>
    public static CredentialCeremonyView Of(CredentialCeremony ceremony)
    {
        ArgumentNullException.ThrowIfNull(ceremony);

        return new CredentialCeremonyView(
            ceremony.RelyingPartyId,
            ceremony.Algorithms,
            ceremony.DiscoverableCredential,
            ceremony.Challenge);
    }
}
