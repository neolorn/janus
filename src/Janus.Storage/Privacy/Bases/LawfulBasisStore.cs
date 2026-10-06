using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Janus.Privacy.Bases;

namespace Janus.Storage.Privacy.Bases;

/// <summary>
/// The declared lawful bases, over the <c>lawful_bases</c> table.
/// </summary>
/// <param name="connections">The connection and transaction the operation runs on.</param>
/// <remarks>Implements PRIV-BASIS-001 and CONV-DESIGN-003.</remarks>
internal sealed class LawfulBasisStore(DataConnections connections) : ILawfulBasisStore
{
    // PRIV-BASIS-001: the lock conflicts with itself and with every row write, so two
    // starts write one after the other and the later one's list stands whole.
    private const string Hold = "LOCK TABLE identity.lawful_bases IN SHARE ROW EXCLUSIVE MODE;";

    private const string Write =
        """
        INSERT INTO identity.lawful_bases
            (key, label, is_consent, requires_written_consent_for_sensitive, requires_assessment, is_objectable)
        VALUES
            (@Key, @Label, @IsConsent, @RequiresWrittenConsentForSensitive, @RequiresAssessment, @IsObjectable)
        ON CONFLICT (key) DO UPDATE SET
            label = EXCLUDED.label,
            is_consent = EXCLUDED.is_consent,
            requires_written_consent_for_sensitive = EXCLUDED.requires_written_consent_for_sensitive,
            requires_assessment = EXCLUDED.requires_assessment,
            is_objectable = EXCLUDED.is_objectable;
        """;

    private const string Remove = "DELETE FROM identity.lawful_bases WHERE NOT (key = ANY(@keys));";

    /// <inheritdoc/>
    public async ValueTask ReplaceAsync(
        IReadOnlyList<LawfulBasisDeclaration> declared,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(declared);

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        if (ambient.Transaction is null)
        {
            throw new InvalidOperationException("The lawful bases are written only inside the operation's transaction.");
        }

        _ = await ambient.Connection
            .ExecuteAsync(new CommandDefinition(Hold, transaction: ambient.Transaction, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        foreach (LawfulBasisDeclaration basis in declared)
        {
            _ = await ambient.Connection
                .ExecuteAsync(new CommandDefinition(
                    Write,
                    new
                    {
                        basis.Key,
                        basis.Label,
                        basis.IsConsent,
                        basis.RequiresWrittenConsentForSensitive,
                        basis.RequiresAssessment,
                        basis.IsObjectable,
                    },
                    ambient.Transaction,
                    cancellationToken: cancellationToken))
                .ConfigureAwait(false);
        }

        _ = await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                Remove,
                new { keys = declared.Select(basis => basis.Key).ToArray() },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }
}
