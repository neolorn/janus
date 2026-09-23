using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Recovery;
using Janus.Core;
using Janus.Storage.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Recovery;

/// <summary>
/// The loss reports that are running, over the <c>loss_reports</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="keyEncryptionKeys">The versions a subject key may be wrapped under.</param>
/// <param name="randomness">The randomness each initialisation vector is drawn from.</param>
/// <remarks>
/// Implements AUTH-RECOV-007, PRIV-RIGHT-005a and CONV-DESIGN-003. The cancel token
/// is held rather than fingerprinted, because every repeat of the notice carries the
/// same link, so it is held under the account's own key.
/// </remarks>
internal sealed class LossReportStore(
    StoreContext context,
    KeyEncryptionKeys keyEncryptionKeys,
    RandomNumberGenerator randomness) : ILossReportStore
{
    /// <inheritdoc/>
    public async ValueTask<LossReport?> FindAsync(
        AuthenticatorId credential,
        CancellationToken cancellationToken) =>
        await ReadAsync(
                await context.LossReports
                    .FindAsync([credential], cancellationToken)
                    .ConfigureAwait(false),
                cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask AddAsync(LossReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);

        await context.LossReports
            .AddAsync(
                new LossReportRecord
                {
                    Credential = report.Credential,
                    Subject = report.Subject,
                    Cancel = await HeldAsync(report, cancellationToken).ConfigureAwait(false),
                    ReportedAt = report.ReportedAt,
                    InvalidatesAt = report.InvalidatesAt,
                    NotifiedAt = report.NotifiedAt,
                    AnyDelivered = report.AnyDelivered,
                    HeldAt = report.HeldAt,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask RecordAsync(LossReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);

        LossReportRecord record = await context.LossReports
            .FindAsync([report.Credential], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The loss report has no row to carry the change.");

        record.NotifiedAt = report.NotifiedAt;
        record.AnyDelivered = report.AnyDelivered;
        record.HeldAt = report.HeldAt;
    }

    /// <inheritdoc/>
    public async ValueTask RemoveAsync(
        AuthenticatorId credential,
        CancellationToken cancellationToken)
    {
        LossReportRecord? record = await context.LossReports
            .FindAsync([credential], cancellationToken)
            .ConfigureAwait(false);

        if (record is not null)
        {
            context.LossReports.Remove(record);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<LossReport>> OutstandingAsync(
        DateTimeOffset notifiedBefore,
        DateTimeOffset invalidatingBefore,
        CancellationToken cancellationToken)
    {
        List<LossReportRecord> outstanding = await context.LossReports
            .Where(report =>
                report.NotifiedAt <= notifiedBefore || report.InvalidatesAt <= invalidatingBefore)
            .OrderBy(report => report.InvalidatesAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var reports = new List<LossReport>(outstanding.Count);

        foreach (LossReportRecord record in outstanding)
        {
            reports.Add(
                await ReadAsync(record, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The loss report read back as nothing."));
        }

        return reports;
    }

    private static PersonalFieldLocation Located(SubjectId subject) =>
        new(subject, LossReportConfiguration.Table, LossReportConfiguration.CancelColumn);

    private async ValueTask<byte[]> HeldAsync(
        LossReport report,
        CancellationToken cancellationToken)
    {
        byte[] dataKey = await DataKeyAsync(report.Subject, cancellationToken).ConfigureAwait(false);

        try
        {
            return PersonalFieldCipher.Encrypt(
                dataKey,
                Located(report.Subject),
                report.Cancel,
                randomness);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    private async ValueTask<LossReport?> ReadAsync(
        LossReportRecord? record,
        CancellationToken cancellationToken)
    {
        if (record is null)
        {
            return null;
        }

        byte[] dataKey = await DataKeyAsync(record.Subject, cancellationToken).ConfigureAwait(false);
        byte[] cancel;

        try
        {
            cancel = PersonalFieldCipher.Decrypt(dataKey, Located(record.Subject), record.Cancel);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }

        return LossReport.Existing(
            record.Credential,
            record.Subject,
            cancel,
            record.ReportedAt,
            record.InvalidatesAt,
            record.NotifiedAt,
            record.AnyDelivered,
            record.HeldAt);
    }

    private async ValueTask<byte[]> DataKeyAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        SubjectKeyRecord key = await context.SubjectKeys
            .FindAsync([subject], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The subject has no key to hold a token under.");

        return PersonalFieldCipher.Unwrap(key.FormatMarker, key.KeyVersion, key.WrappedKey, keyEncryptionKeys);
    }
}
