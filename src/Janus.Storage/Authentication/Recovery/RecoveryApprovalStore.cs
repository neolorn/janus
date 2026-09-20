using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Recovery;
using Janus.Core;
using Janus.Storage.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Recovery;

/// <summary>
/// The approvals standing behind an account's re-enrolment, over the
/// <c>recovery_approvals</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="keyEncryptionKeys">The versions a subject key may be wrapped under.</param>
/// <param name="randomness">The randomness each initialisation vector is drawn from.</param>
/// <remarks>
/// Implements AUTH-RECOV-002, AUTH-RECOV-003, PRIV-RIGHT-005a and CONV-DESIGN-003.
/// Both day limits count rows and not the standing set, so an approval the link has
/// already spent still counts against the day it was given.
/// </remarks>
internal sealed class RecoveryApprovalStore(
    JanusDbContext context,
    KeyEncryptionKeys keyEncryptionKeys,
    RandomNumberGenerator randomness) : IRecoveryApprovalStore
{
    /// <inheritdoc/>
    public async ValueTask AddAsync(RecoveryApproval approval, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(approval);

        await context.RecoveryApprovals
            .AddAsync(
                new RecoveryApprovalRecord
                {
                    Subject = approval.Subject,
                    Approver = approval.Approver,
                    At = approval.At,
                    Channel = await HeldAsync(approval, cancellationToken).ConfigureAwait(false),
                    SpentAt = approval.SpentAt,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<RecoveryApproval>> StandingForAsync(
        SubjectId subject,
        DateTimeOffset from,
        CancellationToken cancellationToken)
    {
        List<RecoveryApprovalRecord> standing = await context.RecoveryApprovals
            .Where(approval =>
                approval.Subject == subject && approval.At >= from && approval.SpentAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (standing.Count == 0)
        {
            return [];
        }

        byte[] dataKey = await DataKeyAsync(subject, cancellationToken).ConfigureAwait(false);

        try
        {
            return [.. standing.Select(approval => new RecoveryApproval(
                approval.Subject,
                approval.Approver,
                Encoding.UTF8.GetString(
                    PersonalFieldCipher.Decrypt(dataKey, Located(subject), approval.Channel)),
                approval.At,
                approval.SpentAt))];
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<int> ForAsync(
        SubjectId subject,
        DateTimeOffset from,
        CancellationToken cancellationToken) =>
        await context.RecoveryApprovals
            .CountAsync(
                approval => approval.Subject == subject && approval.At >= from,
                cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<int> ByAsync(
        SubjectId approver,
        DateTimeOffset from,
        CancellationToken cancellationToken) =>
        await context.RecoveryApprovals
            .CountAsync(
                approval => approval.Approver == approver && approval.At >= from,
                cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask SpendAsync(
        SubjectId subject,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        List<RecoveryApprovalRecord> standing = await context.RecoveryApprovals
            .Where(approval => approval.Subject == subject && approval.SpentAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (RecoveryApprovalRecord approval in standing)
        {
            approval.SpentAt = at;
        }
    }

    private static PersonalFieldLocation Located(SubjectId subject) =>
        new(subject, RecoveryApprovalConfiguration.Table, RecoveryApprovalConfiguration.ChannelColumn);

    private async ValueTask<byte[]> HeldAsync(
        RecoveryApproval approval,
        CancellationToken cancellationToken)
    {
        byte[] dataKey = await DataKeyAsync(approval.Subject, cancellationToken).ConfigureAwait(false);

        try
        {
            return PersonalFieldCipher.Encrypt(
                dataKey,
                Located(approval.Subject),
                Encoding.UTF8.GetBytes(approval.Channel),
                randomness);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    private async ValueTask<byte[]> DataKeyAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        SubjectKeyRecord key = await context.SubjectKeys
            .FindAsync([subject], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The subject has no key to hold a channel under.");

        return PersonalFieldCipher.Unwrap(key.FormatMarker, key.KeyVersion, key.WrappedKey, keyEncryptionKeys);
    }
}
