using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Invitations;
using Janus.Core;

namespace Janus.Authentication.Organizations;

/// <summary>
/// The reads of the organizations a deployment holds: the organizations a caller may
/// read, one of them, its current members and its standing invitations.
/// </summary>
/// <param name="admission">Where a read of an organization is admitted.</param>
/// <param name="directory">Where organizations and their members are read.</param>
/// <param name="invitations">Where an organization's invitations are read.</param>
/// <param name="volume">Where the members a person reads are counted.</param>
/// <param name="time">The clock an invitation's expiry is judged on.</param>
/// <remarks>
/// Implements IDN-READ-001, IDN-READ-002, IDN-READ-004 and chapter 09 section 8a
/// (D-218, D-219). A read whose path names an organization is admitted where its
/// permission is held there or in the administrative organization, as one admission. An
/// organization the deployment never held, or erased, is answered for what it is before
/// the permission is asked of it (09 section 8a). A read is not audited; a refusal is
/// recorded as every refusal is.
/// </remarks>
internal sealed class OrganizationReads(
    IReadAdmission admission,
    IOrganizationDirectory directory,
    IInvitationStore invitations,
    IReadVolume volume,
    TimeProvider time)
{
    private const int DefaultLimit = 100;

    private const int MostLimit = 500;

    /// <summary>
    /// Lists the organizations the caller may read, one page at a time.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="after">The identifier the page begins after, or nothing.</param>
    /// <param name="limit">At most how many, or nothing for the default.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The page, or the refusal.</returns>
    /// <exception cref="ArgumentNullException">The context is absent.</exception>
    public async ValueTask<Result<OrganizationPage>> ListAsync(
        AccessContext context,
        OrganizationId? after,
        int? limit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (Paged(limit) is not int count)
        {
            return Result.Failure<OrganizationPage>(Malformed("limit"));
        }

        return await (await admission
                .ListedAsync(context, Permissions.OrganizationRead, cancellationToken)
                .ConfigureAwait(false))
            .Match(
                readable => PageAsync(readable, after, count, cancellationToken),
                error => ValueTask.FromResult(Result.Failure<OrganizationPage>(error)))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Reads one organization.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="organization">Which organization.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The organization, or the refusal.</returns>
    /// <exception cref="ArgumentNullException">The context is absent.</exception>
    public async ValueTask<Result<OrganizationSummary>> ReadAsync(
        AccessContext context,
        OrganizationId organization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await ScopeOfAsync(organization, cancellationToken).ConfigureAwait(false) is not OrganizationId held)
        {
            return Result.Failure<OrganizationSummary>(Error.From(ErrorCodes.OrganizationNotFound));
        }

        if (await admission
                .RefusedAsync(context, Permissions.OrganizationRead, held, cancellationToken)
                .ConfigureAwait(false) is Error refused)
        {
            return Result.Failure<OrganizationSummary>(refused);
        }

        return await directory.FindAsync(held, cancellationToken).ConfigureAwait(false) is { ErasedAt: null } standing
            ? Result.Success(Summary(standing))
            : Result.Failure<OrganizationSummary>(Error.From(ErrorCodes.OrganizationNotFound));
    }

    /// <summary>
    /// Lists an organization's current members, one page at a time.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="organization">Of which organization.</param>
    /// <param name="after">The subject the page begins after, or nothing.</param>
    /// <param name="limit">At most how many, or nothing for the default.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The page, or the refusal.</returns>
    /// <exception cref="ArgumentNullException">The context is absent.</exception>
    public async ValueTask<Result<MemberPage>> MembersAsync(
        AccessContext context,
        OrganizationId organization,
        SubjectId? after,
        int? limit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (Paged(limit) is not int count)
        {
            return Result.Failure<MemberPage>(Malformed("limit"));
        }

        if (await RefusedAsync(context, organization, cancellationToken).ConfigureAwait(false) is Error refused)
        {
            return Result.Failure<MemberPage>(refused);
        }

        IReadOnlyList<MemberSummary> page = await directory
            .MemberPageAsync(organization, after, count + 1, cancellationToken)
            .ConfigureAwait(false);
        var members = new MemberPage([.. page.Take(count)], page.Count > count ? page[count - 1].Subject : null);

        return await CountedAsync(context, members, members.Members.Count, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads one account's current membership of an organization.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="organization">Of which organization.</param>
    /// <param name="member">Whose membership.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The member, or the refusal.</returns>
    /// <exception cref="ArgumentNullException">The context is absent.</exception>
    public async ValueTask<Result<MemberSummary>> MemberAsync(
        AccessContext context,
        OrganizationId organization,
        SubjectId member,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await RefusedAsync(context, organization, cancellationToken).ConfigureAwait(false) is Error refused)
        {
            return Result.Failure<MemberSummary>(refused);
        }

        // IDN-READ-002 AC2: a membership that ended is no current membership.
        if (await directory.MemberAsync(organization, member, cancellationToken).ConfigureAwait(false)
            is not MemberSummary found)
        {
            return Result.Failure<MemberSummary>(Error.From(ErrorCodes.MembershipNotFound));
        }

        return await CountedAsync(context, found, 1, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Lists an organization's standing invitations.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="organization">Of which organization.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The invitations, or the refusal.</returns>
    /// <exception cref="ArgumentNullException">The context is absent.</exception>
    public async ValueTask<Result<IReadOnlyList<StandingInvitation>>> StandingAsync(
        AccessContext context,
        OrganizationId organization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await RefusedAsync(context, organization, cancellationToken).ConfigureAwait(false) is Error refused)
        {
            return Result.Failure<IReadOnlyList<StandingInvitation>>(refused);
        }

        // IDN-READ-002 AC3: an invitation acknowledged, revoked or past its lifetime does
        // not stand.
        IReadOnlyList<Invitation> standing = await invitations
            .StandingAsync(organization, time.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);

        return Result.Success<IReadOnlyList<StandingInvitation>>(
            [
                .. standing.Select(invitation => new StandingInvitation(
                    invitation.Id,
                    invitation.Roles,
                    invitation.IssuedAt,
                    invitation.ExpiresAt,
                    invitation.Inviter,
                    invitation.Identifiers?.Email,
                    invitation.Identifiers?.Phone,
                    invitation.Identifiers?.CorporateEmail)),
            ]);
    }

    private static OrganizationSummary Summary(OrganizationStanding standing) =>
        new(standing.Id, standing.Name, standing.IsAdministrative, standing.DeletionRequestedAt);

    // 09 section 8a: a limit is 1 to 500, and 100 where none is given.
    private static int? Paged(int? limit) =>
        limit switch
        {
            null => DefaultLimit,
            >= 1 and <= MostLimit => limit,
            _ => null,
        };

    private static Error Malformed(string member) =>
        Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement(member));

    // IDN-READ-001 (D-219): every organization the deployment holds where the caller may
    // read them all, otherwise those it was admitted in; an erased one is never listed.
    private async ValueTask<Result<OrganizationPage>> PageAsync(
        ReadableOrganizations readable,
        OrganizationId? after,
        int count,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<OrganizationStanding> page = await directory
            .PageAsync(readable.Every ? null : readable.Organizations, after, count + 1, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new OrganizationPage(
            [.. page.Take(count).Select(Summary)],
            page.Count > count ? page[count - 1].Id : null));
    }

    // AUTHZ-SCOPE-001, 09 section 8a: where the read is asked, the organization the path
    // names, where the deployment holds it and has not erased it.
    private async ValueTask<OrganizationId?> ScopeOfAsync(
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        await directory.FindAsync(organization, cancellationToken).ConfigureAwait(false) is { ErasedAt: null }
            ? organization
            : null;

    // IDN-READ-002, IDN-READ-004 (D-219): the organization is answered for what it is,
    // then membership:read is admitted in it or in the administrative organization.
    private async ValueTask<Error?> RefusedAsync(
        AccessContext context,
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        await ScopeOfAsync(organization, cancellationToken).ConfigureAwait(false) is OrganizationId held
            ? await admission
                .RefusedAsync(context, Permissions.MembershipRead, held, cancellationToken)
                .ConfigureAwait(false)
            : Error.From(ErrorCodes.OrganizationNotFound);

    // OPS-ALERT-005: each member a person reads counts toward their read volume; a
    // system principal's reads are nobody's and are not counted.
    private async ValueTask<Result<TRead>> CountedAsync<TRead>(
        AccessContext context,
        TRead read,
        int records,
        CancellationToken cancellationToken) =>
        (await volume.ReturnedAsync(context, records, cancellationToken).ConfigureAwait(false))
            .Match(() => Result.Success(read), Result.Failure<TRead>);
}
