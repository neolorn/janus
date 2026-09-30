using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
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
/// <param name="connections">The connection and transaction the operation holds.</param>
/// <param name="time">The clock the identifiers are drawn from.</param>
/// <remarks>
/// Implements REG-INV-001, IDN-LIFE-009a, IDN-MEM-002 and CONV-LAYOUT-001. Whether
/// the account may hold another membership is the membership's own rule, decided under
/// a lock on the account's row so two attachments made together decide one after the
/// other; the database holds one current membership of an organization per account
/// whatever writes it. Each grant is made as any stored grant is.
/// </remarks>
internal sealed class MembershipAttachment(
    IMembershipStore memberships,
    IGrantStore grants,
    DataConnections connections,
    TimeProvider time) : IMembershipAttachment
{
    private const string Hold =
        """
        SELECT 1 FROM identity.accounts WHERE subject = @subject FOR UPDATE;
        """;

    /// <inheritdoc/>
    public async ValueTask<Error?> RefusedAsync(
        SubjectId subject,
        OrganizationId organization,
        bool multiple,
        CancellationToken cancellationToken) =>
        Membership.Refused(
            subject,
            organization,
            await memberships.FindBySubjectAsync(subject, cancellationToken).ConfigureAwait(false),
            multiple);

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    /// <exception cref="InvalidOperationException">No transaction is running.</exception>
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

        // IDN-MEM-002, X3 of D-166: the memberships are read, and the new one decided,
        // under the account's row lock, so a second attachment waits for this one to
        // commit and decides on what it made.
        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        if (ambient.Transaction is null)
        {
            throw new InvalidOperationException("A membership is attached only inside the operation's transaction.");
        }

        _ = await ambient.Connection
            .ExecuteScalarAsync<int?>(new CommandDefinition(
                Hold,
                new { subject = subject.Value },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

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
        // so a role named twice is granted once here rather than by the check below. A
        // role stands in for the invitation's only where the account holds it live
        // across the organization with no expiry, as the invitation grants it.
        IReadOnlyList<Grant> standing = await grants
            .HeldByAsync([GrantSubject.Of(subject)], organization, at, cancellationToken)
            .ConfigureAwait(false);

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

            if (!standing.Any(held => held.Subject == grant.Subject
                && held.Role == role
                && held.ResourceType is null
                && !held.Deny
                && held.ExpiresAt is null))
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
