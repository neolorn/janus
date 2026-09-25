using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Invitations;
using Janus.Authorization.Grants;
using Janus.Core;
using Janus.Identity.Organizations;

namespace Janus.Storage.Authentication.Invitations;

/// <summary>
/// The membership an acknowledged invitation attaches and the grants it carries, over
/// the <c>memberships</c> and <c>grants</c> tables.
/// </summary>
/// <param name="memberships">Where the account's memberships are read and the new one written.</param>
/// <param name="grants">Where the grants are written.</param>
/// <param name="time">The clock the identifiers are drawn from.</param>
/// <remarks>
/// Implements REG-INV-001, IDN-LIFE-009a, IDN-MEM-002 and CONV-LAYOUT-001. Whether
/// the account may hold another membership is the membership's own rule, and each
/// grant is made as any stored grant is.
/// </remarks>
internal sealed class MembershipAttachment(
    IMembershipStore memberships,
    IGrantStore grants,
    TimeProvider time) : IMembershipAttachment
{
    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async ValueTask<Result<MembershipId>> AttachAsync(
        SubjectId subject,
        OrganizationId organization,
        IReadOnlyList<InvitationDocument> acknowledged,
        IReadOnlyList<RoleName> roles,
        SubjectId grantedBy,
        string reason,
        bool multiple,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(acknowledged);
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(reason);

        IReadOnlyList<Membership> held = await memberships
            .FindBySubjectAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        Error? failure = null;

        Membership membership = Membership
            .Create(
                MembershipId.New(time),
                subject,
                organization,
                held,
                multiple,
                at,
                new MembershipAcknowledgement(acknowledged, at))
            .Match(made => made, error => Withheld<Membership>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<MembershipId>(failure);
        }

        await memberships.CreateAsync(membership, cancellationToken).ConfigureAwait(false);

        // The grants are written in the transaction and read back only once it commits,
        // so a role named twice is granted once here rather than by the check below.
        foreach (RoleName role in roles.Distinct())
        {
            Grant grant = Grant
                .Create(
                    GrantId.New(time),
                    GrantSubject.Of(subject),
                    role,
                    organization,
                    on: null,
                    deny: false,
                    GrantKind.Stored,
                    expiresAt: null,
                    grantedBy,
                    at,
                    reason)
                .Match(
                    created => created,
                    error => throw new InvalidOperationException(error.Code.ToString()));

            if (!await grants.ExistsAsync(grant, at, cancellationToken).ConfigureAwait(false))
            {
                await grants.CreateAsync(grant, cancellationToken).ConfigureAwait(false);
            }
        }

        return Result.Success(membership.Id);
    }

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
