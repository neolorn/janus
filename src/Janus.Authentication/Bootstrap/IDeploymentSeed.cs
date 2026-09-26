using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Registration;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Bootstrap;

/// <summary>
/// What a fresh deployment is seeded with: its named values, its administrative roles,
/// its administrative organization and the accounts bootstrap creates in it.
/// </summary>
/// <remarks>
/// Implements OPS-BOOT-001, OPS-BOOT-002, chapter 10 section 3 and IDN-ORG-001. Each
/// method writes inside the caller's transaction, so a bootstrap that is refused part
/// of the way leaves nothing behind.
/// </remarks>
internal interface IDeploymentSeed
{
    /// <summary>
    /// Whether the deployment has been bootstrapped already: the administrative
    /// organization exists, or some grant confers a role that administers the
    /// deployment.
    /// </summary>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>Whether a system administrator exists.</returns>
    ValueTask<bool> AdministeredAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Writes the values bootstrap sets, protected keys among them, in the form the
    /// settings table holds them, and records each under the principal that set it.
    /// </summary>
    /// <param name="written">The written form of each value, by its key.</param>
    /// <param name="principal">The principal bootstrap runs as.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of writing them, which later reads in the transaction see.</returns>
    ValueTask ConfigureAsync(
        IReadOnlyDictionary<ConfigurationKey, string> written,
        SystemPrincipal principal,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Defines each role that is not defined yet, and records each under the principal
    /// that defined it; a role the deployment defined already is left as it stands.
    /// </summary>
    /// <param name="defined">The permissions of each role, by its name.</param>
    /// <param name="principal">The principal bootstrap runs as.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of defining them.</returns>
    ValueTask DefineRolesAsync(
        IReadOnlyDictionary<RoleName, IReadOnlyList<Permission>> defined,
        SystemPrincipal principal,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates the administrative organization, the one row bootstrap alone marks so.
    /// </summary>
    /// <param name="organization">Its identifier.</param>
    /// <param name="name">Its name.</param>
    /// <param name="at">When it was created.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of creating it.</returns>
    ValueTask CreateOrganizationAsync(
        OrganizationId organization,
        string name,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates an account for a subject, with the subject key its fields are encrypted
    /// under and the identifiers it holds, each verified.
    /// </summary>
    /// <param name="subject">Whose account.</param>
    /// <param name="held">What it holds, the first of each kind primary.</param>
    /// <param name="name">The display name it shows, where it shows one.</param>
    /// <param name="at">When it was created.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of creating it.</returns>
    ValueTask CreateAccountAsync(
        SubjectId subject,
        IReadOnlyList<NewIdentifier> held,
        DisplayName? name,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates the reserved <c>emergency</c> account, which holds no identifier and no
    /// sign-in method of any kind.
    /// </summary>
    /// <param name="subject">Its subject identifier.</param>
    /// <param name="at">When it was created.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of creating it.</returns>
    ValueTask CreateEmergencyAsync(SubjectId subject, DateTimeOffset at, CancellationToken cancellationToken);
}
