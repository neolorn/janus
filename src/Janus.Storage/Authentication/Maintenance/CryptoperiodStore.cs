using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Bootstrap;
using Janus.Authentication.Maintenance;
using Janus.Core;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Identity.Audit;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Maintenance;

/// <summary>
/// The records of the audit trail the key-encryption key's cryptoperiod is measured
/// from.
/// </summary>
/// <param name="context">The context the records are read through.</param>
/// <remarks>Implements DR-009a and CONV-DESIGN-003.</remarks>
internal sealed class CryptoperiodStore(StoreContext context) : ICryptoperiodStore
{
    // IDN-PRIN-001: a rotation and bootstrap are recorded under a system principal, beside
    // the nil subject, so the reads take the index on the acting subject.
    private static readonly SubjectId Nobody = new(Guid.Empty);

    // OPS-SEC-003 AC5, chapter 10 section 5.41: the kind a completion names.
    private static readonly string KeyEncryptionKey = VocabularyConverter<KeyRotationKind>.Write(KeyRotationKind.KeyEncryptionKey);

    /// <inheritdoc/>
    public async ValueTask<(int Version, DateTimeOffset CompletedAt)?> KeyEncryptionKeyRotatedAsync(
        CancellationToken cancellationToken)
    {
        List<AuditRowRecord> completions = await context.AuditRecords
            .AsNoTracking()
            .Where(row => row.ActingSubject == Nobody && row.Action == AuditActions.KeyRotationCompleted)
            .OrderByDescending(row => row.OccurredAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (AuditRowRecord completion in completions)
        {
            using var details = JsonDocument.Parse(completion.Details);

            if (details.RootElement.TryGetProperty("kind", out JsonElement kind)
                && kind.ValueEquals(KeyEncryptionKey)
                && details.RootElement.TryGetProperty("version", out JsonElement version))
            {
                return (version.GetInt32(), completion.OccurredAt);
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public async ValueTask<DateTimeOffset?> BootstrappedAsync(CancellationToken cancellationToken) =>
        await context.AuditRecords
            .AsNoTracking()
            .Where(row => row.ActingSubject == Nobody
                && row.Action == AuditActions.OrganizationCreated
                && row.Principal == DeploymentBootstrap.PrincipalName)
            .OrderBy(row => row.OccurredAt)
            .Select(row => (DateTimeOffset?)row.OccurredAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
}
