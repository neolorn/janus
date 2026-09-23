using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Policies;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Organizations;

/// <summary>
/// Whether an email address may be used by a member of the organizations that lock
/// their members to verified domains.
/// </summary>
/// <param name="memberships">Which organizations the account belongs to.</param>
/// <param name="configuration">Where each organization's list is read.</param>
/// <param name="domains">Where each listed domain's verification stands.</param>
/// <remarks>
/// Implements REG-DOM-001, IDN-ORG-006 and REG-MAIL-003. The lock is the organization's,
/// so it reaches an account through a current membership and through nothing else: it
/// stops applying the moment the membership ends. Each organization's lock is judged on
/// its own, so an account in two is held to both. A domain removed from a lock goes on
/// refusing until it is listed anew, which is what stops new sign-ins with addresses in
/// it even where the removal left the lock empty.
/// </remarks>
internal sealed class DomainLock(
    IMembershipLookup memberships,
    IConfigurationStore configuration,
    IDomainStore domains)
{
    /// <summary>
    /// Judges one address for one account.
    /// </summary>
    /// <param name="subject">Whose address it is.</param>
    /// <param name="address">The address.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Nothing where every lock the account is under admits the address, or the
    /// refusal: <c>identity.identifier.domainnotallowed</c>, or the failure that kept a
    /// lock from being read.
    /// </returns>
    public async ValueTask<Error?> RefusedAsync(
        SubjectId subject,
        EmailAddress address,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<OrganizationId> organizations = await memberships
            .OfAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        bool reads = DomainName.TryReadOf(address, out string domain);

        foreach (OrganizationId organization in organizations)
        {
            Error? failure = null;

            PolicyOverride stated = (await configuration
                    .ReadAsync(Settings.OrganizationPolicy, organization.ToString(), cancellationToken)
                    .ConfigureAwait(false))
                .Match(value => value, error => Withheld<PolicyOverride>(error, ref failure));

            if (failure is not null)
            {
                return failure;
            }

            IReadOnlyList<LockedDomain> held = await domains
                .OfAsync(organization, cancellationToken)
                .ConfigureAwait(false);

            if (!Admits(stated.EmailDomains ?? [], held, reads ? domain : null))
            {
                return Error.From(ErrorCodes.IdentifierDomainNotAllowed);
            }
        }

        return null;
    }

    // An address whose domain does not read is admitted only where nothing locks it.
    private static bool Admits(IReadOnlyList<string> listed, IReadOnlyList<LockedDomain> held, string? domain)
    {
        bool removed = held.Any(row => !row.IsListed && string.Equals(row.Domain, domain, StringComparison.Ordinal))
            && !listed.Contains(domain, StringComparer.Ordinal);

        if (removed)
        {
            return false;
        }

        return listed.Count is 0
            || (domain is not null
                && listed.Contains(domain, StringComparer.Ordinal)
                && held.Any(row => row.Admits && string.Equals(row.Domain, domain, StringComparison.Ordinal)));
    }

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
