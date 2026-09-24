using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Factors;

/// <summary>
/// Where enrolled credentials are read and written. Secrets and keys are held
/// encrypted at rest, so a dump of the table yields no usable credential.
/// </summary>
/// <remarks>Implements AUTH-FACT-006 and CONV-DESIGN-003.</remarks>
internal interface IAuthenticatorStore
{
    /// <summary>
    /// Reads one credential.
    /// </summary>
    /// <param name="id">Which credential.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The credential, or nothing where there is none.</returns>
    ValueTask<Authenticator?> FindAsync(AuthenticatorId id, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the credential an authenticator answered with, which is how a
    /// discoverable credential is resolved: the browser names the credential and not
    /// the account.
    /// </summary>
    /// <param name="credentialId">What the authenticator returned.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The credential, or nothing where there is none.</returns>
    ValueTask<Authenticator?> ByCredentialAsync(
        ReadOnlyMemory<byte> credentialId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads the credential a social provider's identity is linked as, which is how a
    /// provider's security event is resolved: the provider names its own subject and
    /// not the account (IDN-LIFE-012a, REG-IDENT-008).
    /// </summary>
    /// <param name="provider">Which social provider.</param>
    /// <param name="subject">The provider's subject identifier.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The credential, or nothing where there is none.</returns>
    ValueTask<Authenticator?> ByProviderAsync(
        Factor provider,
        string subject,
        CancellationToken cancellationToken);

    /// <summary>
    /// Every credential of an account, whatever its state.
    /// </summary>
    /// <param name="subject">Whose credentials.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The credentials.</returns>
    ValueTask<IReadOnlyList<Authenticator>> OfAsync(
        SubjectId subject,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records a newly enrolled credential.
    /// </summary>
    /// <param name="authenticator">The credential.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask AddAsync(Authenticator authenticator, CancellationToken cancellationToken);

    /// <summary>
    /// Records a newly linked social provider's identity, under the provider's subject
    /// identifier it is found by from then on.
    /// </summary>
    /// <param name="authenticator">The credential.</param>
    /// <param name="subject">The provider's subject identifier.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask LinkAsync(
        Authenticator authenticator,
        string subject,
        CancellationToken cancellationToken);

    /// <summary>
    /// Carries a change a credential made onto its row.
    /// </summary>
    /// <param name="authenticator">The credential as it now stands.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask RecordAsync(Authenticator authenticator, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a credential outright, which an abandoned enrolment, a removal that
    /// lowers nothing and a provider's withdrawal of a linked identity all do.
    /// </summary>
    /// <param name="id">Which credential.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of removing it.</returns>
    ValueTask RemoveAsync(AuthenticatorId id, CancellationToken cancellationToken);
}
