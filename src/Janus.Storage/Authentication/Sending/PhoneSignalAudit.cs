using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Identity.Audit;

namespace Janus.Storage.Authentication.Sending;

/// <summary>
/// What considering a number leaves in the audit trail before a restricted factor
/// goes to it.
/// </summary>
/// <param name="records">Where the trail is appended to.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements AUTH-FACT-002b and IDN-AUD-001. Where the deployment registered no
/// provider this record is the whole of what happens, so it is what an operator reads
/// to decide whether to register one.
/// </remarks>
internal sealed class PhoneSignalAudit(IAuditStore records, TimeProvider time) : IPhoneSignalAudit
{
    private const string Absent = "unavailable";

    private static readonly AuditAction Considered =
        AuditActions.PhoneSignalConsidered;

    /// <inheritdoc/>
    public async ValueTask ConsideredAsync(
        Factor factor,
        PhoneSignal? signal,
        SubjectId? subject,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        await records
            .AppendAsync(
                AuditRecord.Of(
                    AuditRecordId.New(time),
                    AuditCategory.Security,
                    Considered,
                    at,
                    // A sign-in link goes to a number before anything has established
                    // whose it is, so there is not always an account to name.
                    subject ?? default,
                    default,
                    organization: null,
                    new Dictionary<string, JsonElement>(capacity: 2, StringComparer.Ordinal)
                    {
                        ["factor"] = JsonSerializer.SerializeToElement(
                            VocabularyConverter<Factor>.Write(factor)),
                        ["signal"] = JsonSerializer.SerializeToElement(
                            signal is PhoneSignal answered
                                ? VocabularyConverter<PhoneSignal>.Write(answered)
                                : Absent),
                    }),
                cancellationToken)
            .ConfigureAwait(false);
}
