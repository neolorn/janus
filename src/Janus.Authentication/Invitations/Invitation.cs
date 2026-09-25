using System;
using System.Collections.Generic;
using Janus.Authentication.Mailboxes;
using Janus.Core;

namespace Janus.Authentication.Invitations;

/// <summary>
/// One invitation into an organization: what it binds, what attaches when the person
/// acknowledges it, and how far it has got.
/// </summary>
/// <remarks>
/// Implements IDN-LIFE-009a, REG-INV-001, REG-INV-002 and REG-MAIL-001. The link opens
/// it once: it attaches to the registration or the account that opened it, and no
/// other. The identifiers it binds are someone's personal data before any account of
/// theirs exists, so they are forgotten the moment the invitation is revoked or
/// acknowledged; what stays is who invited whom into what, which the membership and
/// the audit trail answer to.
/// </remarks>
internal sealed class Invitation
{
    private Invitation(
        InvitationId id,
        OrganizationId organization,
        SubjectId inviter,
        [NeverLogged] byte[] token,
        IReadOnlyList<RoleName> roles,
        IReadOnlyList<InvitationDocument> documents,
        MailboxId? mailbox,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt)
    {
        Id = id;
        Organization = organization;
        Inviter = inviter;
        Token = token;
        Roles = roles;
        Documents = documents;
        Mailbox = mailbox;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
    }

    /// <summary>The invitation's own identifier.</summary>
    public InvitationId Id { get; }

    /// <summary>Into which organization.</summary>
    public OrganizationId Organization { get; }

    /// <summary>Who issued it.</summary>
    public SubjectId Inviter { get; }

    /// <summary>
    /// What is stored against the link's token, which the token cannot be recovered
    /// from.
    /// </summary>
    [NeverLogged]
    public byte[] Token { get; }

    /// <summary>
    /// What it binds, or nothing once it has been revoked or acknowledged.
    /// </summary>
    public InvitedIdentifiers? Identifiers { get; private set; }

    /// <summary>The roles granted across the organization when the membership attaches.</summary>
    public IReadOnlyList<RoleName> Roles { get; }

    /// <summary>The documents the person is shown and acknowledges.</summary>
    public IReadOnlyList<InvitationDocument> Documents { get; }

    /// <summary>The corporate mailbox reserved for it, where the organization's mail is integrated.</summary>
    public MailboxId? Mailbox { get; }

    /// <summary>When it was issued.</summary>
    public DateTimeOffset IssuedAt { get; }

    /// <summary>When its link stops opening it, and when it can no longer be acknowledged.</summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>The registration it attached to, while that registration runs.</summary>
    public RegistrationSessionId? Session { get; private set; }

    /// <summary>
    /// The account it attached to: the one that opened it while signed in, or the one
    /// the registration it attached to created.
    /// </summary>
    public SubjectId? Invitee { get; private set; }

    /// <summary>When its link was opened, which used it.</summary>
    public DateTimeOffset? AttachedAt { get; private set; }

    /// <summary>When the person acknowledged it and the membership attached.</summary>
    public DateTimeOffset? AcknowledgedAt { get; private set; }

    /// <summary>When it was revoked, or replaced by a later invitation.</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>Whether it was revoked.</summary>
    public bool IsRevoked => RevokedAt is not null;

    /// <summary>Whether it was acknowledged.</summary>
    public bool IsAcknowledged => AcknowledgedAt is not null;

    /// <summary>
    /// Whether it still stands: neither revoked nor acknowledged, expired or not. An
    /// expired invitation that stands keeps its mailbox reserved (REG-MAIL-001).
    /// </summary>
    public bool Stands => !IsRevoked && !IsAcknowledged;

    /// <summary>
    /// An invitation as it is issued.
    /// </summary>
    /// <param name="id">The identifier issued for it.</param>
    /// <param name="organization">Into which organization.</param>
    /// <param name="inviter">Who issues it.</param>
    /// <param name="identifiers">What it binds.</param>
    /// <param name="roles">The roles that attach with the membership.</param>
    /// <param name="documents">The documents the person acknowledges.</param>
    /// <param name="mailbox">The mailbox reserved for it, where one was.</param>
    /// <param name="token">What is stored against the link's token.</param>
    /// <param name="issuedAt">When it is issued.</param>
    /// <param name="lifetime">How long its link opens it.</param>
    /// <returns>The invitation, open.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static Invitation Issued(
        InvitationId id,
        OrganizationId organization,
        SubjectId inviter,
        InvitedIdentifiers identifiers,
        IReadOnlyList<RoleName> roles,
        IReadOnlyList<InvitationDocument> documents,
        MailboxId? mailbox,
        [NeverLogged] byte[] token,
        DateTimeOffset issuedAt,
        TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(identifiers);
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(token);

        return new Invitation(
            id,
            organization,
            inviter,
            token,
            roles,
            documents,
            mailbox,
            issuedAt,
            issuedAt + lifetime)
        {
            Identifiers = identifiers,
        };
    }

    /// <summary>
    /// The invitation as it already stands. This is the store's translation of a
    /// stored row and no step anyone took.
    /// </summary>
    /// <param name="id">Which invitation.</param>
    /// <param name="organization">Into which organization.</param>
    /// <param name="inviter">Who issued it.</param>
    /// <param name="token">What is stored against the link's token.</param>
    /// <param name="identifiers">What it binds, where it is still kept.</param>
    /// <param name="roles">The roles that attach with the membership.</param>
    /// <param name="documents">The documents the person acknowledges.</param>
    /// <param name="mailbox">The mailbox reserved for it, where one was.</param>
    /// <param name="issuedAt">When it was issued.</param>
    /// <param name="expiresAt">When its link stops opening it.</param>
    /// <param name="session">The registration it attached to, where one runs.</param>
    /// <param name="invitee">The account it attached to, where one did.</param>
    /// <param name="attachedAt">When its link was opened, where it was.</param>
    /// <param name="acknowledgedAt">When it was acknowledged, where it was.</param>
    /// <param name="revokedAt">When it was revoked, where it was.</param>
    /// <returns>The invitation.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static Invitation Existing(
        InvitationId id,
        OrganizationId organization,
        SubjectId inviter,
        [NeverLogged] byte[] token,
        InvitedIdentifiers? identifiers,
        IReadOnlyList<RoleName> roles,
        IReadOnlyList<InvitationDocument> documents,
        MailboxId? mailbox,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        RegistrationSessionId? session,
        SubjectId? invitee,
        DateTimeOffset? attachedAt,
        DateTimeOffset? acknowledgedAt,
        DateTimeOffset? revokedAt)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(documents);

        return new Invitation(
            id,
            organization,
            inviter,
            token,
            roles,
            documents,
            mailbox,
            issuedAt,
            expiresAt)
        {
            Identifiers = identifiers,
            Session = session,
            Invitee = invitee,
            AttachedAt = attachedAt,
            AcknowledgedAt = acknowledgedAt,
            RevokedAt = revokedAt,
        };
    }

    /// <summary>
    /// Whether its lifetime has run out.
    /// </summary>
    /// <param name="now">The instant to judge it at.</param>
    /// <returns>Whether it has.</returns>
    public bool HasExpired(DateTimeOffset now) => now >= ExpiresAt;

    /// <summary>
    /// Whether its link opens it now: it stands, it has not expired, and nothing has
    /// opened it before.
    /// </summary>
    /// <param name="now">The instant the link is presented.</param>
    /// <returns>Whether it does.</returns>
    public bool Opens(DateTimeOffset now) => Stands && AttachedAt is null && !HasExpired(now);

    /// <summary>
    /// Attaches it to the registration its link opened.
    /// </summary>
    /// <param name="session">The registration.</param>
    /// <param name="at">When the link was opened.</param>
    /// <exception cref="InvalidOperationException">The link no longer opens it.</exception>
    public void AttachTo(RegistrationSessionId session, DateTimeOffset at)
    {
        Opened(at);

        Session = session;
    }

    /// <summary>
    /// Attaches it to the account that opened its link while signed in.
    /// </summary>
    /// <param name="invitee">The account.</param>
    /// <param name="at">When the link was opened.</param>
    /// <exception cref="InvalidOperationException">The link no longer opens it.</exception>
    public void AttachTo(SubjectId invitee, DateTimeOffset at)
    {
        Opened(at);

        Invitee = invitee;
    }

    /// <summary>
    /// Carries it from the registration it attached to onto the account that
    /// registration created.
    /// </summary>
    /// <param name="invitee">The account.</param>
    /// <exception cref="InvalidOperationException">It is attached to no registration.</exception>
    public void Registered(SubjectId invitee)
    {
        if (Session is null || Invitee is not null)
        {
            throw new InvalidOperationException("The invitation is attached to no registration.");
        }

        Session = null;
        Invitee = invitee;
    }

    /// <summary>
    /// Records that the person it is attached to acknowledged it and the membership
    /// attached, forgetting what it bound.
    /// </summary>
    /// <param name="at">When.</param>
    /// <exception cref="InvalidOperationException">
    /// It no longer stands, it is attached to no account, or it has expired.
    /// </exception>
    public void Acknowledge(DateTimeOffset at)
    {
        if (!Stands || Invitee is null || HasExpired(at))
        {
            throw new InvalidOperationException("The invitation cannot be acknowledged.");
        }

        AcknowledgedAt = at;
        Identifiers = null;
    }

    /// <summary>
    /// Forgets what it bound once it has expired unused. It still names who invited
    /// into what, and when, and its mailbox stays reserved.
    /// </summary>
    /// <param name="now">The instant it is judged at.</param>
    /// <exception cref="InvalidOperationException">It has not expired.</exception>
    public void Lapse(DateTimeOffset now)
    {
        if (!HasExpired(now))
        {
            throw new InvalidOperationException("The invitation has not expired.");
        }

        Identifiers = null;
    }

    /// <summary>
    /// Revokes it, forgetting what it bound.
    /// </summary>
    /// <param name="at">When.</param>
    /// <exception cref="InvalidOperationException">It was acknowledged.</exception>
    public void Revoke(DateTimeOffset at)
    {
        if (IsAcknowledged)
        {
            throw new InvalidOperationException("An acknowledged invitation is used.");
        }

        RevokedAt ??= at;
        Identifiers = null;
    }

    private void Opened(DateTimeOffset at)
    {
        if (!Opens(at))
        {
            throw new InvalidOperationException("The invitation's link no longer opens it.");
        }

        AttachedAt = at;
    }
}
