using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Identity.Audit;

namespace Janus.Storage.Authentication.Sending;

/// <summary>
/// What a bot-defence signal leaves in the audit trail, whether or not a challenge
/// was shown.
/// </summary>
/// <param name="records">Where the trail is appended to.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements AUTH-ABUSE-008 and IDN-AUD-001. Where no verifier is registered this
/// record is the whole of what happens, so it is what the operator reads to decide
/// whether to register one.
/// </remarks>
internal sealed class BotDefenceAudit(IAuditStore records, TimeProvider time) : IBotDefenceAudit
{
    private static readonly AuditAction Signalled = AuditActions.BotDefenceSignalled;

    /// <inheritdoc/>
    public async ValueTask SignalledAsync(
        BotDefenceSignal signal,
        string source,
        bool challenged,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        await records
            .AppendAsync(
                AuditRecord.Of(
                    AuditRecordId.New(time),
                    AuditCategory.Security,
                    Signalled,
                    at,
                    // A signal fires part-way through a registration, so there is no
                    // account yet for the record to name (AUTH-ABUSE-008).
                    default,
                    default,
                    organization: null,
                    new Dictionary<string, JsonElement>(capacity: 2, StringComparer.Ordinal)
                    {
                        ["signal"] = JsonSerializer.SerializeToElement(
                            VocabularyConverter<BotDefenceSignal>.Write(signal)),
                        ["challenged"] = JsonSerializer.SerializeToElement(challenged),
                    }),
                cancellationToken)
            .ConfigureAwait(false);
}
