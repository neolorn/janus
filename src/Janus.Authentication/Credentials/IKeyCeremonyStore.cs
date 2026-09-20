using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Credentials;

/// <summary>
/// Where the creation ceremony an account has open is held. One stands per account.
/// </summary>
/// <remarks>Implements AUTH-FACT-014 and CONV-DESIGN-003.</remarks>
internal interface IKeyCeremonyStore
{
    /// <summary>
    /// The ceremony an account has open.
    /// </summary>
    /// <param name="subject">Whose account.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The ceremony, or nothing where none is open.</returns>
    ValueTask<KeyCeremony?> FindAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// Opens a ceremony, replacing whatever the account had open: the previous
    /// challenge stops answering, so an abandoned ceremony is never a second way in.
    /// </summary>
    /// <param name="ceremony">The ceremony.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of opening it.</returns>
    ValueTask ReplaceAsync(KeyCeremony ceremony, CancellationToken cancellationToken);

    /// <summary>
    /// Closes the ceremony an account had open, which completing one does.
    /// </summary>
    /// <param name="subject">Whose account.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of closing it.</returns>
    ValueTask RemoveAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// Removes every ceremony that has stopped answering.
    /// </summary>
    /// <param name="now">Now.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>How many were removed.</returns>
    ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
