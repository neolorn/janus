using System;
using Janus.Core;

namespace Janus.Authentication.Credentials;

/// <summary>
/// A creation ceremony an account has open: what it is creating, the value the
/// authenticator signs over, and the entry it replaces where it is an upgrade.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-002b, AUTH-FACT-014 and AUTH-STEP-007. The challenge is
/// server-issued and remembered here, because a ceremony whose challenge the caller
/// chose proves nothing. One stands per account: opening another replaces it.
/// </remarks>
internal sealed class KeyCeremony
{
    private KeyCeremony(
        SubjectId subject,
        Factor kind,
        string challenge,
        AuthenticatorId? upgrading,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt)
    {
        Subject = subject;
        Kind = kind;
        Challenge = challenge;
        Upgrading = upgrading;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
    }

    /// <summary>Whose account it is open on.</summary>
    public SubjectId Subject { get; }

    /// <summary>Which catalogue entry it creates.</summary>
    public Factor Kind { get; }

    /// <summary>The value the authenticator signs over.</summary>
    public string Challenge { get; }

    /// <summary>The second-factor entry it replaces, where it is an upgrade.</summary>
    public AuthenticatorId? Upgrading { get; }

    /// <summary>When it was opened.</summary>
    public DateTimeOffset IssuedAt { get; }

    /// <summary>When it stops answering.</summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>
    /// The ceremony as the store holds it.
    /// </summary>
    /// <param name="subject">Whose account.</param>
    /// <param name="kind">Which catalogue entry it creates.</param>
    /// <param name="challenge">The value the authenticator signs over.</param>
    /// <param name="upgrading">The entry it replaces, or nothing.</param>
    /// <param name="issuedAt">When it was opened.</param>
    /// <param name="expiresAt">When it stops answering.</param>
    /// <returns>The ceremony.</returns>
    public static KeyCeremony Existing(
        SubjectId subject,
        Factor kind,
        string challenge,
        AuthenticatorId? upgrading,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt) =>
        new(subject, kind, challenge, upgrading, issuedAt, expiresAt);

    /// <summary>
    /// Whether it has stopped answering.
    /// </summary>
    /// <param name="now">Now.</param>
    /// <returns>Whether it has.</returns>
    public bool HasExpired(DateTimeOffset now) => now >= ExpiresAt;
}
