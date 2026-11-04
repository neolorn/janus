using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;

namespace Janus.Storage.Authentication.Search;

/// <summary>
/// The database channel a write that changes what the staff search holds is announced
/// on.
/// </summary>
/// <remarks>
/// Implements IDN-SRCH-003 and chapter 10 section 5.54 (D-256). The announcement is made
/// inside the transaction of the write, so the database releases it when that
/// transaction commits and never for one that rolls back. It carries the subject's
/// identifier and nothing else: a listener reads the subject back for itself.
/// </remarks>
internal static class SearchChannel
{
    /// <summary>
    /// What a listener listens on.
    /// </summary>
    public const string Name = "identity_search";

    private const string Announce = "SELECT pg_notify(@channel, @subject);";

    /// <summary>
    /// Announces that what the search holds of a subject may have changed.
    /// </summary>
    /// <param name="context">The context whose connection and transaction the write runs on.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of announcing it.</returns>
    public static async ValueTask RaiseAsync(
        StoreContext context,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        AmbientConnection ambient = await new DataConnections(context).UseAsync(cancellationToken).ConfigureAwait(false);

        _ = await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                Announce,
                new { channel = Name, subject = subject.ToString() },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }
}
