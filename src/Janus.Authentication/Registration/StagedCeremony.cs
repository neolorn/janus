using System;
using Janus.Core;

namespace Janus.Authentication.Registration;

/// <summary>
/// The WebAuthn creation ceremony a registration session has open: what it creates,
/// the value the authenticator signs over, and when it stops answering.
/// </summary>
/// <param name="Kind">Which catalogue entry it creates.</param>
/// <param name="Challenge">The value the authenticator signs over.</param>
/// <param name="ExpiresAt">When it stops answering.</param>
/// <remarks>
/// Implements REG-SESS-001 and AUTH-FACT-014. It is a staged value of the session and
/// of no other record, since no account exists for a ceremony to be open on. One
/// stands per session: opening another replaces it.
/// </remarks>
internal sealed record StagedCeremony(Factor Kind, string Challenge, DateTimeOffset ExpiresAt)
{
    /// <summary>
    /// Whether it has stopped answering.
    /// </summary>
    /// <param name="now">Now.</param>
    /// <returns>Whether it has.</returns>
    public bool HasExpired(DateTimeOffset now) => now >= ExpiresAt;
}
