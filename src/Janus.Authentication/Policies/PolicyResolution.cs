using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Policies;

/// <summary>
/// The one policy a principal resolves to, read from their organization membership
/// and from nothing else.
/// </summary>
/// <param name="memberships">Which organizations a principal belongs to.</param>
/// <param name="configuration">Where the system policy and the overrides are read from.</param>
/// <remarks>Implements AUTH-PRIN-002 and AUTH-STEP-002a.</remarks>
internal sealed class PolicyResolution(
    IMembershipLookup memberships,
    IConfigurationStore configuration)
{
    /// <summary>
    /// The policy in force for a principal.
    /// </summary>
    /// <param name="subject">The principal.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The system policy for a principal holding no membership, their organization's
    /// for one holding a membership, and the strictest of several for one holding
    /// several.
    /// </returns>
    public async ValueTask<Result<Policy>> ForAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        Policy system = (await configuration.ReadAsync(Settings.PolicyDefault, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<Policy>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<Policy>(failure);
        }

        IReadOnlyList<OrganizationId> held =
            await memberships.OfAsync(subject, cancellationToken).ConfigureAwait(false);

        Policy? organizations = null;

        foreach (OrganizationId organization in held)
        {
            PolicyOverride overrides =
                (await configuration.ReadAsync(
                        Settings.OrganizationPolicy,
                        organization.ToString(),
                        cancellationToken)
                    .ConfigureAwait(false))
                .Match(value => value, error => Held<PolicyOverride>(error, ref failure));

            if (failure is not null)
            {
                return Result.Failure<Policy>(failure);
            }

            Policy theirs = PolicyStrictness.Tighten(system, overrides);

            organizations = organizations is null
                ? theirs
                : PolicyStrictness.Strictest(organizations, theirs);
        }

        return Result.Success(organizations ?? system);
    }

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
