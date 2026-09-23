using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;

namespace Janus.Storage.Authentication.Registration;

/// <summary>
/// The database channel a registration session's changes are announced on.
/// </summary>
/// <remarks>
/// Implements REG-SESS-003 and FE-VER-001. The announcement is made inside the
/// transaction that changed the session, so the database releases it when that
/// transaction commits and never for one that rolls back. What it carries is the
/// session's identifier and nothing else: a listener reads the state back for itself,
/// under the access every other read of it goes through.
/// </remarks>
internal static class RegistrationChannel
{
    /// <summary>
    /// What a listener listens on.
    /// </summary>
    public const string Name = "identity_registration";

    private const string Announce = "SELECT pg_notify(@channel, @session);";

    /// <summary>
    /// Announces that a session has changed.
    /// </summary>
    /// <param name="ambient">The operation's connection and transaction.</param>
    /// <param name="session">The session that changed.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of announcing it.</returns>
    public static async ValueTask RaiseAsync(
        AmbientConnection ambient,
        RegistrationSessionId session,
        CancellationToken cancellationToken)
    {
        DbConnection connection = ambient.Connection;

        _ = await connection
            .ExecuteAsync(new CommandDefinition(
                Announce,
                new { channel = Name, session = session.ToString() },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }
}
