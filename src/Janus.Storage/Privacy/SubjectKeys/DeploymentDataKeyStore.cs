using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Janus.Privacy.SubjectKeys;

namespace Janus.Storage.Privacy.SubjectKeys;

/// <summary>
/// The deployment's data key, over its row of the subject-key table under the max UUID,
/// which no subject is issued and erasure never touches. Every value the library
/// encrypts that belongs to no subject is encrypted, or has its own data key wrapped,
/// under it.
/// </summary>
/// <param name="connections">The connection and transaction the operation holds.</param>
/// <param name="keyEncryptionKeys">The versions the key is wrapped under.</param>
/// <param name="randomness">The randomness the key is drawn from, the first time it is needed.</param>
/// <remarks>
/// Implements PRIV-RIGHT-005a, AUTH-KEY-002 and OPS-SEC-003 (D-166, 316; D-172). The
/// key-encryption key wraps it as it wraps every subject key, so a rotation re-wraps rows
/// of the one table and nothing else. The row is written the first time a value needs
/// it, by an insert that does nothing on conflict, so two processes writing it together
/// both read the one that stands.
/// </remarks>
internal sealed class DeploymentDataKeyStore(
    DataConnections connections,
    KeyEncryptionKeys keyEncryptionKeys,
    RandomNumberGenerator randomness)
{
    private const string Held =
        """
        SELECT format_marker, key_version, wrapped_key
        FROM identity.subject_keys
        WHERE subject = @subject;
        """;

    private const string Written =
        """
        INSERT INTO identity.subject_keys (subject, format_marker, key_version, wrapped_key)
        VALUES (@subject, @marker, @version, @wrapped)
        ON CONFLICT (subject) DO NOTHING;
        """;

    /// <summary>
    /// The key, unwrapped, written first where the deployment has none yet.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The plaintext key, which the caller clears when its method ends.</returns>
    public async ValueTask<byte[]> UnwrappedAsync(CancellationToken cancellationToken)
    {
        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<(short Marker, int Version, byte[] Wrapped)> held = await HeldAsync(ambient, cancellationToken)
            .ConfigureAwait(false);

        if (held.Count is 0)
        {
            byte[] dataKey = PersonalFieldCipher.NewDataKey(randomness);

            try
            {
                _ = await ambient.Connection
                    .ExecuteAsync(new CommandDefinition(
                        Written,
                        new
                        {
                            subject = SubjectKeyId.Deployment.Value,
                            marker = (short)PersonalDataFormat.Marker,
                            version = keyEncryptionKeys.CurrentVersion,
                            wrapped = PersonalFieldCipher.Wrap(dataKey, keyEncryptionKeys.Current.Span),
                        },
                        ambient.Transaction,
                        cancellationToken: cancellationToken))
                    .ConfigureAwait(false);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(dataKey);
            }

            held = await HeldAsync(ambient, cancellationToken).ConfigureAwait(false);
        }

        (short marker, int version, byte[] wrapped) = held.Count is 1
            ? held[0]
            : throw new InvalidOperationException("The deployment's data key was written and is not there to read.");

        return PersonalFieldCipher.Unwrap((byte)marker, version, wrapped, keyEncryptionKeys);
    }

    private static async ValueTask<IReadOnlyList<(short Marker, int Version, byte[] Wrapped)>> HeldAsync(
        AmbientConnection ambient,
        CancellationToken cancellationToken) =>
        [
            .. await ambient.Connection
                .QueryAsync<(short, int, byte[])>(new CommandDefinition(
                    Held,
                    new { subject = SubjectKeyId.Deployment.Value },
                    ambient.Transaction,
                    cancellationToken: cancellationToken))
                .ConfigureAwait(false),
        ];
}
