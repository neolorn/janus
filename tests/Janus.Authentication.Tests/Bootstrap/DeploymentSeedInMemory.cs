using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Bootstrap;
using Janus.Authentication.Registration;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Tests.Bootstrap;

/// <summary>
/// What bootstrap writes that no other operation does, held in memory.
/// </summary>
/// <remarks>
/// CONV-TEST-004: a fake that keeps what it was given, and answers whether the
/// deployment is administered as a test says it is.
/// </remarks>
internal sealed class DeploymentSeedInMemory : IDeploymentSeed
{
    /// <summary>
    /// Whether the deployment has a system administrator already.
    /// </summary>
    public bool Administered { get; set; }

    /// <summary>
    /// Every key written, with the value it was given last.
    /// </summary>
    public Dictionary<ConfigurationKey, string> Configured { get; } = [];

    /// <summary>
    /// Every role defined.
    /// </summary>
    public List<RoleName> Roles { get; } = [];

    /// <summary>
    /// Every organization created.
    /// </summary>
    public List<OrganizationId> Organizations { get; } = [];

    /// <summary>
    /// Every account created, the reserved one included.
    /// </summary>
    public List<SubjectId> Accounts { get; } = [];

    /// <inheritdoc/>
    public ValueTask<bool> AdministeredAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Administered);

    /// <inheritdoc/>
    public ValueTask ConfigureAsync(
        IReadOnlyDictionary<ConfigurationKey, string> written,
        IReadOnlyDictionary<ConfigurationKey, string> defaults,
        SystemPrincipal principal,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(written);

        foreach (KeyValuePair<ConfigurationKey, string> value in written)
        {
            Configured[value.Key] = value.Value;
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask DefineRolesAsync(
        IReadOnlyDictionary<RoleName, IReadOnlyList<Permission>> defined,
        SystemPrincipal principal,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(defined);

        Roles.AddRange(defined.Keys);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask CreateOrganizationAsync(
        OrganizationId organization,
        string name,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        Organizations.Add(organization);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask CreateAccountAsync(
        SubjectId subject,
        IReadOnlyList<NewIdentifier> held,
        DisplayName? name,
        bool? adultAffirmed,
        DateOnly? dateOfBirth,
        AgeGroup? group,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        Accounts.Add(subject);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask CreateEmergencyAsync(SubjectId subject, DateTimeOffset at, CancellationToken cancellationToken)
    {
        Accounts.Add(subject);

        return ValueTask.CompletedTask;
    }
}
