using System;
using Janus.Core;

namespace Janus.Authentication.Accounts;

/// <summary>
/// The one link an account's own lifecycle notice carries, held by its fingerprint
/// so that a database dump yields no usable token.
/// </summary>
/// <remarks>
/// Implements IDN-LIFE-013, IDN-LIFE-014 and D-147. It has no lifetime of its own:
/// it lives as long as the state it ends, and the state ending takes it with it.
/// </remarks>
internal sealed class LifecycleLink
{
    private LifecycleLink(
        SubjectId subject,
        LifecycleLinkKind kind,
        byte[] token,
        DateTimeOffset issuedAt)
    {
        Subject = subject;
        Kind = kind;
        Token = token;
        IssuedAt = issuedAt;
    }

    /// <summary>
    /// Whose account it ends the state of.
    /// </summary>
    public SubjectId Subject { get; }

    /// <summary>
    /// Which state it ends.
    /// </summary>
    public LifecycleLinkKind Kind { get; }

    /// <summary>
    /// What the token in the notice hashes to.
    /// </summary>
    public byte[] Token { get; }

    /// <summary>
    /// When the notice carrying it went out.
    /// </summary>
    public DateTimeOffset IssuedAt { get; }

    /// <summary>
    /// The link a notice about to go out carries.
    /// </summary>
    /// <param name="subject">Whose account.</param>
    /// <param name="kind">Which state it ends.</param>
    /// <param name="token">The token the notice carries.</param>
    /// <param name="at">When it was issued.</param>
    /// <returns>The link.</returns>
    public static LifecycleLink Issued(
        SubjectId subject,
        LifecycleLinkKind kind,
        OpaqueToken token,
        DateTimeOffset at) =>
        new(subject, kind, token.Fingerprint(), at);

    /// <summary>
    /// The link as the row already holds it.
    /// </summary>
    /// <param name="subject">Whose account.</param>
    /// <param name="kind">Which state it ends.</param>
    /// <param name="token">What the token hashes to.</param>
    /// <param name="issuedAt">When it was issued.</param>
    /// <returns>The link.</returns>
    /// <exception cref="ArgumentNullException">The fingerprint is absent.</exception>
    public static LifecycleLink Existing(
        SubjectId subject,
        LifecycleLinkKind kind,
        byte[] token,
        DateTimeOffset issuedAt)
    {
        ArgumentNullException.ThrowIfNull(token);

        return new LifecycleLink(subject, kind, token, issuedAt);
    }
}
