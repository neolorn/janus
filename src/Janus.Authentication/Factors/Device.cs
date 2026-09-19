using System;
using Janus.Core;

namespace Janus.Authentication.Factors;

/// <summary>
/// One browser an account knows: a trusted device, whose trust stands in for the
/// second factor of a sign-in, or a browser the new-device check has seen.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-015 and AUTH-FACT-016. It is never a claim and never a
/// session: what it stands in for is one step of one sign-in, and it raises nothing
/// on the session that follows.
/// </remarks>
internal sealed class Device
{
    private Device(
        DeviceId id,
        SubjectId subject,
        DeviceKind kind,
        CredentialLabel label,
        DateTimeOffset at,
        DateTimeOffset expires)
    {
        Id = id;
        Subject = subject;
        Kind = kind;
        Label = label;
        CreatedAt = at;
        LastUsedAt = at;
        ExpiresAt = expires;
    }

    /// <summary>Which browser.</summary>
    public DeviceId Id { get; }

    /// <summary>Whose it is.</summary>
    public SubjectId Subject { get; }

    /// <summary>What it is known for.</summary>
    public DeviceKind Kind { get; }

    /// <summary>What the person calls it.</summary>
    public CredentialLabel Label { get; }

    /// <summary>When it became known.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>When it was last seen.</summary>
    public DateTimeOffset LastUsedAt { get; private set; }

    /// <summary>When what it stands for lapses.</summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>
    /// How many sign-ins on it have failed in a row, which is what revokes the trust
    /// of a browser somebody else is sitting at (AUTH-FACT-015).
    /// </summary>
    public int ConsecutiveFailures { get; private set; }

    /// <summary>Whether it was revoked before it lapsed.</summary>
    public bool Revoked { get; private set; }

    /// <summary>
    /// A browser the account now knows.
    /// </summary>
    /// <param name="id">The identifier issued for it.</param>
    /// <param name="subject">Whose it is.</param>
    /// <param name="kind">What it is known for.</param>
    /// <param name="label">What the person calls it.</param>
    /// <param name="at">When it became known.</param>
    /// <param name="lifetime">How long what it stands for lasts.</param>
    /// <returns>The browser.</returns>
    public static Device Known(
        DeviceId id,
        SubjectId subject,
        DeviceKind kind,
        CredentialLabel label,
        DateTimeOffset at,
        TimeSpan lifetime) =>
        new(id, subject, kind, label, at, at + lifetime);

    /// <summary>
    /// The browser as the account already knows it, which is the store's translation
    /// of a row and no change to it.
    /// </summary>
    /// <param name="id">Which browser.</param>
    /// <param name="subject">Whose it is.</param>
    /// <param name="kind">What it is known for.</param>
    /// <param name="label">What the person calls it.</param>
    /// <param name="createdAt">When it became known.</param>
    /// <param name="lastUsedAt">When it was last seen.</param>
    /// <param name="expiresAt">When what it stands for lapses.</param>
    /// <param name="consecutiveFailures">How many sign-ins on it failed in a row.</param>
    /// <param name="revoked">Whether it was revoked before it lapsed.</param>
    /// <returns>The browser.</returns>
    public static Device Existing(
        DeviceId id,
        SubjectId subject,
        DeviceKind kind,
        CredentialLabel label,
        DateTimeOffset createdAt,
        DateTimeOffset lastUsedAt,
        DateTimeOffset expiresAt,
        int consecutiveFailures,
        bool revoked) =>
        new(id, subject, kind, label, createdAt, expiresAt)
        {
            LastUsedAt = lastUsedAt,
            ConsecutiveFailures = consecutiveFailures,
            Revoked = revoked,
        };

    /// <summary>
    /// Whether what it stands for holds at this instant.
    /// </summary>
    /// <param name="now">The instant.</param>
    /// <returns>Whether it holds.</returns>
    public bool Stands(DateTimeOffset now) => !Revoked && now < ExpiresAt;

    /// <summary>
    /// It was presented and accepted, which also clears the failures behind it.
    /// </summary>
    /// <param name="at">When.</param>
    public void Used(DateTimeOffset at)
    {
        LastUsedAt = at;
        ConsecutiveFailures = 0;
    }

    /// <summary>
    /// A sign-in on it failed. Enough failures in a row are somebody guessing at a
    /// browser whose trust would spare them the second factor, so the trust goes.
    /// </summary>
    /// <param name="limit">How many in a row the deployment allows.</param>
    public void Failed(int limit)
    {
        ConsecutiveFailures++;

        if (ConsecutiveFailures >= limit)
        {
            Revoke();
        }
    }

    /// <summary>
    /// The person removed it, or something that revokes every one of them happened.
    /// </summary>
    public void Revoke() => Revoked = true;
}
