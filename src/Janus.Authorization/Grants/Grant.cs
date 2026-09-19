using System;
using Janus.Core;

namespace Janus.Authorization.Grants;

/// <summary>
/// One sentence: a subject has a role on a resource. Sharing a record, making someone
/// a branch manager, granting a group access to a folder are all this sentence with
/// different nouns, and there is no other kind of permission.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GRANT-001, AUTHZ-GRANT-002 and AUTHZ-GRANT-003. A grant with no
/// resource scopes to the whole organization. A deny is this same row with a flag, and
/// it defeats any allow without a priority number anywhere. Expiry is read where the
/// grant is read, so an expired grant confers nothing whether or not a sweep has run.
/// </remarks>
internal sealed class Grant
{
    private Grant(
        GrantId id,
        GrantSubject subject,
        RoleName role,
        OrganizationId organization,
        ResourceType? resourceType,
        ResourceId? resourceId,
        bool deny,
        GrantKind kind,
        DateTimeOffset? expiresAt,
        SubjectId grantedBy,
        DateTimeOffset grantedAt,
        string reason)
    {
        Id = id;
        Subject = subject;
        Role = role;
        Organization = organization;
        ResourceType = resourceType;
        ResourceId = resourceId;
        Deny = deny;
        Kind = kind;
        ExpiresAt = expiresAt;
        GrantedBy = grantedBy;
        GrantedAt = grantedAt;
        Reason = reason;
    }

    /// <summary>
    /// The identifier a revocation and an explanation name.
    /// </summary>
    public GrantId Id { get; }

    /// <summary>
    /// Who holds it: one account, or a group whose members hold it transitively.
    /// </summary>
    public GrantSubject Subject { get; }

    /// <summary>
    /// The role, whose permissions are read live so that editing the role takes effect
    /// at once.
    /// </summary>
    public RoleName Role { get; }

    /// <summary>
    /// The organization the grant is scoped to, resolved from the resource and never
    /// from a session.
    /// </summary>
    public OrganizationId Organization { get; }

    /// <summary>
    /// The kind of thing it is on, or nothing where it is on the whole organization.
    /// </summary>
    public ResourceType? ResourceType { get; }

    /// <summary>
    /// The record it is on, or nothing where it is on the whole organization.
    /// </summary>
    public ResourceId? ResourceId { get; }

    /// <summary>
    /// Whether it takes access away rather than conferring it.
    /// </summary>
    public bool Deny { get; }

    /// <summary>
    /// Whether someone wrote it, a fact in the host's data produced it, or it was
    /// precomputed from such a fact.
    /// </summary>
    public GrantKind Kind { get; }

    /// <summary>
    /// When it stops conferring anything, where it was given an expiry.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; }

    /// <summary>
    /// Who granted it.
    /// </summary>
    public SubjectId GrantedBy { get; }

    /// <summary>
    /// When it was granted.
    /// </summary>
    public DateTimeOffset GrantedAt { get; }

    /// <summary>
    /// Why it was granted, recorded because "who granted this and why" is a question
    /// asked long afterwards.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Who revoked it, where it was revoked.
    /// </summary>
    public SubjectId? RevokedBy { get; private set; }

    /// <summary>
    /// When it was revoked.
    /// </summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>
    /// Why it was revoked.
    /// </summary>
    public string? RevocationReason { get; private set; }

    /// <summary>
    /// Whether it is on the whole organization rather than on one record.
    /// </summary>
    public bool IsOrganizationWide => ResourceType is null;

    /// <summary>
    /// A new grant.
    /// </summary>
    /// <param name="id">The identifier issued for it.</param>
    /// <param name="subject">Who holds it.</param>
    /// <param name="role">The role it confers.</param>
    /// <param name="organization">The organization it is scoped to.</param>
    /// <param name="on">The record it is on, or nothing for the whole organization.</param>
    /// <param name="deny">Whether it takes access away rather than conferring it.</param>
    /// <param name="kind">Where it came from.</param>
    /// <param name="expiresAt">When it stops conferring anything, where it expires.</param>
    /// <param name="grantedBy">Who granted it.</param>
    /// <param name="grantedAt">When it was granted.</param>
    /// <param name="reason">Why it was granted.</param>
    /// <returns>The grant, or the refusal a blank reason carries.</returns>
    /// <exception cref="ArgumentException">The reason is longer than a free-text field.</exception>
    public static Result<Grant> Create(
        GrantId id,
        GrantSubject subject,
        RoleName role,
        OrganizationId organization,
        ResourceReference? on,
        bool deny,
        GrantKind kind,
        DateTimeOffset? expiresAt,
        SubjectId grantedBy,
        DateTimeOffset grantedAt,
        string reason)
    {
        if (!Stated(reason, out string stated))
        {
            return Result.Failure<Grant>(Error.From(ErrorCodes.GrantReasonRequired));
        }

        return Result.Success(new Grant(
            id,
            subject,
            role,
            organization,
            on?.Type,
            on?.Id,
            deny,
            kind,
            expiresAt,
            grantedBy,
            grantedAt,
            stated));
    }

    /// <summary>
    /// A grant as its row holds it.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="subject">Who holds it.</param>
    /// <param name="role">The role it confers.</param>
    /// <param name="organization">The organization it is scoped to.</param>
    /// <param name="on">The record it is on, or nothing for the whole organization.</param>
    /// <param name="deny">Whether it takes access away.</param>
    /// <param name="kind">Where it came from.</param>
    /// <param name="expiresAt">When it stops conferring anything.</param>
    /// <param name="grantedBy">Who granted it.</param>
    /// <param name="grantedAt">When it was granted.</param>
    /// <param name="reason">Why it was granted.</param>
    /// <param name="revokedBy">Who revoked it.</param>
    /// <param name="revokedAt">When it was revoked.</param>
    /// <param name="revocationReason">Why it was revoked.</param>
    /// <returns>The grant.</returns>
    public static Grant Existing(
        GrantId id,
        GrantSubject subject,
        RoleName role,
        OrganizationId organization,
        ResourceReference? on,
        bool deny,
        GrantKind kind,
        DateTimeOffset? expiresAt,
        SubjectId grantedBy,
        DateTimeOffset grantedAt,
        string reason,
        SubjectId? revokedBy,
        DateTimeOffset? revokedAt,
        string? revocationReason) =>
        new(id, subject, role, organization, on?.Type, on?.Id, deny, kind, expiresAt, grantedBy, grantedAt, reason)
        {
            RevokedBy = revokedBy,
            RevokedAt = revokedAt,
            RevocationReason = revocationReason,
        };

    /// <summary>
    /// Takes the grant back, recording who and why.
    /// </summary>
    /// <param name="by">Who revoked it.</param>
    /// <param name="at">When.</param>
    /// <param name="reason">Why.</param>
    /// <returns>Nothing, or the refusal a blank reason carries.</returns>
    /// <exception cref="InvalidOperationException">The grant is already revoked.</exception>
    /// <exception cref="ArgumentException">The reason is longer than a free-text field.</exception>
    public Result Revoke(SubjectId by, DateTimeOffset at, string reason)
    {
        if (RevokedAt is not null)
        {
            throw new InvalidOperationException("A revoked grant is not revoked again.");
        }

        if (!Stated(reason, out string stated))
        {
            return Result.Failure(Error.From(ErrorCodes.GrantReasonRequired));
        }

        RevokedBy = by;
        RevokedAt = at;
        RevocationReason = stated;

        return Result.Success();
    }

    /// <summary>
    /// Whether the grant confers anything at the given instant: not revoked, and not
    /// past an expiry it was given.
    /// </summary>
    /// <param name="at">The instant.</param>
    /// <returns>Whether it is live.</returns>
    public bool IsLive(DateTimeOffset at) => RevokedAt is null && (ExpiresAt is null || ExpiresAt > at);

    // API-CONV-002 fixes the shape of every free-text field at the boundary; past it a
    // value of another length is a fault rather than a refusal the caller is told about.
    private static bool Stated(string reason, out string stated)
    {
        stated = reason?.Trim() ?? string.Empty;

        if (stated.Length > 1024)
        {
            throw new ArgumentException(
                "A reason is at most 1024 characters after trimming.",
                nameof(reason));
        }

        return stated.Length > 0;
    }
}
