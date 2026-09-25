using System;
using System.Collections.Generic;
using System.Linq;
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
/// <param name="raises">Where the requirements a policy has raised are kept.</param>
/// <remarks>Implements AUTH-PRIN-002, AUTH-STEP-002a and AUTH-FACT-017.</remarks>
internal sealed class PolicyResolution(
    IMembershipLookup memberships,
    IConfigurationStore configuration,
    IPolicyRaiseStore raises)
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
    public ValueTask<Result<Policy>> ForAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        ForAsync(subject, joining: null, cancellationToken);

    /// <summary>
    /// The policy in force for a principal an invitation is taking into an
    /// organization, which that organization's policy governs from the moment the
    /// invitation attaches and before the membership does (IDN-LIFE-009a, REG-INV-001).
    /// </summary>
    /// <param name="subject">The principal, or the subject a registration will create.</param>
    /// <param name="joining">The organization the invitation is into, where one is.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The policy <see cref="ForAsync(SubjectId, CancellationToken)"/> resolves, with the
    /// organization joined counted among the principal's memberships.
    /// </returns>
    public async ValueTask<Result<Policy>> ForAsync(
        SubjectId subject,
        OrganizationId? joining,
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

        List<OrganizationId> held =
            [.. await memberships.OfAsync(subject, cancellationToken).ConfigureAwait(false)];

        if (joining is OrganizationId invitedInto && !held.Contains(invitedInto))
        {
            held.Add(invitedInto);
        }

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

    /// <summary>
    /// What the policies in force over a principal have raised and the principal may
    /// not yet meet.
    /// </summary>
    /// <param name="subject">The principal.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The raises of the deployment's own policy and of every organization the
    /// principal belongs to.
    /// </returns>
    public async ValueTask<IReadOnlyList<PolicyRaise>> RaisesAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        List<PolicyRaise> raised =
            [.. await raises.OfAsync(null, cancellationToken).ConfigureAwait(false)];

        foreach (OrganizationId organization in
            await memberships.OfAsync(subject, cancellationToken).ConfigureAwait(false))
        {
            raised.AddRange(
                await raises.OfAsync(organization, cancellationToken).ConfigureAwait(false));
        }

        return raised;
    }

    /// <summary>
    /// Records what a change to one scope's policy raised. A field the change lowers
    /// or restores stops being raised, because a run-up towards a requirement that no
    /// longer stands would hold a sign-in for nothing (AUTH-FACT-017 AC4).
    /// </summary>
    /// <param name="organization">
    /// Whose policy changed, or nothing for the deployment's own.
    /// </param>
    /// <param name="before">What was in force.</param>
    /// <param name="after">What is in force now.</param>
    /// <param name="at">When the change was made.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>What the change raised, which may be nothing.</returns>
    /// <exception cref="ArgumentNullException">A policy is absent.</exception>
    public async ValueTask<IReadOnlyList<PolicyRaise>> RaisedAsync(
        OrganizationId? organization,
        Policy before,
        Policy after,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<PolicyRaise> raised = PolicyGrace.Raised(before, after, at);

        foreach (PolicyField field in Fields)
        {
            if (!raised.Any(raise => raise.Field == field))
            {
                await raises.RemoveAsync(organization, field, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        await raises.RecordAsync(organization, raised, cancellationToken).ConfigureAwait(false);

        return raised;
    }

    private static readonly PolicyField[] Fields =
        [PolicyField.RequiredAssurance, PolicyField.CredentialRedundancy];

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
