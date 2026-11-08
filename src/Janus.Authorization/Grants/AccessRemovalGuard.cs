using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Janus.Authorization.Grants;

/// <summary>
/// What asks the host's check about a removal of access, inside the change's transaction.
/// </summary>
/// <param name="check">Where the host's check is asked, where it registered one.</param>
/// <param name="locks">Where the removals of an organization are held one after the other.</param>
/// <param name="paths">Which way the change being made came.</param>
/// <param name="refusals">The codes the host's check refused with, which the boundary answers 409.</param>
/// <param name="scopes">Where the scope a refusal is recorded in comes from.</param>
/// <remarks>
/// Implements AUTHZ-GRANT-005 (D-281). Each organization the change touches is held, in
/// the ordinal order of their identifiers, before the check is asked, so the check reads
/// what every removal before it committed and two removals at once are judged one after
/// the other. A refusal is recorded in a scope and a unit of work of its own and handed
/// back with the host's code and details; a check that throws refuses with
/// <c>authz.removal.uncheckable</c> (<see cref="IRemovalCheck"/>), recorded alike, so it
/// never lets a removal through.
/// A refusal under a first segment of the library's catalogue, or <c>session.</c>, is the
/// host's fault and is thrown as one, so the boundary answers <c>system.fault</c> and logs
/// it. With no check registered, nothing is held or asked.
/// </remarks>
internal sealed class AccessRemovalGuard(
    IRemovalCheck check,
    IRemovalLocks locks,
    IRemovalPaths paths,
    RemovalRefusals refusals,
    IServiceScopeFactory scopes)
{
    // D-281 (Q2): the library's prefixes are the first segments the catalogue of 10
    // section 3 uses, and session., which no host code takes either; a test holds the
    // list to the catalogue (CONV-CODE-004 keeps reflection out of the shipped code).
    private static readonly FrozenSet<string> Prefixes = FrozenSet.ToFrozenSet(
        ["api", "auth", "authz", "config", "identity", "integration", "model", "privacy", "session", "system"],
        StringComparer.Ordinal);


    /// <summary>
    /// The library's prefixes, which a test holds to the catalogue.
    /// </summary>
    internal static IReadOnlySet<string> LibraryPrefixes => Prefixes;

    /// <summary>
    /// The way the change being made came, which the removal it describes carries.
    /// </summary>
    public AccessRemovalPath Path => paths.Current;

    /// <summary>
    /// Asks the host's check about one removal, holding first every organization it
    /// touches.
    /// </summary>
    /// <param name="removal">What is about to be removed.</param>
    /// <param name="touched">The organizations the change touches beside the removal's own.</param>
    /// <param name="cancellationToken">Abandons the change.</param>
    /// <returns>Nothing where the removal may go ahead, or the refusal, recorded.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    /// <exception cref="InvalidOperationException">
    /// The check refused under a code of the library's, which is the host's fault.
    /// </exception>
    public async ValueTask<Error?> RefusedAsync(
        AccessRemoval removal,
        IEnumerable<OrganizationId> touched,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(removal);
        ArgumentNullException.ThrowIfNull(touched);

        if (!check.Registered)
        {
            return null;
        }

        await HoldAsync([.. touched, removal.Organization], cancellationToken).ConfigureAwait(false);

        Result answered = await check.AskAsync(removal, cancellationToken).ConfigureAwait(false);

        if (answered.Match<Error?>(() => null, error => error) is not Error refused)
        {
            return null;
        }

        if (refused.Code != ErrorCodes.RemovalUncheckable && Prefixes.Contains(First(refused.Code)))
        {
            throw new InvalidOperationException(
                "The host's access removal check refused under the library's code " + refused.Code + ".");
        }

        if (refused.Code != ErrorCodes.RemovalUncheckable)
        {
            refusals.Record(refused.Code);
        }

        AsyncServiceScope recording = scopes.CreateAsyncScope();

        await using (recording.ConfigureAwait(false))
        {
            await recording.ServiceProvider.GetRequiredService<RemovalRefusalRecording>()
                .RecordAsync(removal, refused.Code, cancellationToken)
                .ConfigureAwait(false);
        }

        return refused;
    }

    /// <summary>
    /// Holds the removals of the organizations given, in the ordinal order of their
    /// identifiers, where the host registered a check: a change takes this before the rows
    /// it locks, so the hold is always taken before them and two changes never wait on
    /// each other the other way round. A second hold of one organization in the
    /// transaction takes nothing further.
    /// </summary>
    /// <param name="organizations">The organizations the change touches.</param>
    /// <param name="cancellationToken">Abandons the change.</param>
    /// <returns>The work of holding them.</returns>
    /// <exception cref="ArgumentNullException">The organizations are absent.</exception>
    public async ValueTask HoldAsync(IEnumerable<OrganizationId> organizations, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(organizations);

        if (!check.Registered)
        {
            return;
        }

        foreach (OrganizationId organization in organizations
            .Distinct()
            .OrderBy(organization => organization.ToString(), StringComparer.Ordinal))
        {
            await locks.HoldAsync(organization, cancellationToken).ConfigureAwait(false);
        }
    }


    private static string First(ErrorCode code)
    {
        string written = code.ToString();

        return written[..written.IndexOf('.', StringComparison.Ordinal)];
    }
}
