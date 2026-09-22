using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Identity.Audit;

namespace Janus.Storage.Authentication.Sending;

/// <summary>
/// What a restriction edit or a grant leaves in the audit trail.
/// </summary>
/// <param name="records">Where the trail is appended to.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements AUTH-ABUSE-004 and IDN-AUD-001. A grant records the restriction, the
/// credit and the reason; the key it was granted to is never written down.
/// </remarks>
internal sealed class SendAudit(IAuditStore records, TimeProvider time) : ISendAudit
{
    private const string HostPrefix = "host:";

    private static readonly JsonSerializerOptions Shape = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private static readonly AuditAction Edited = AuditActions.RestrictionEdited;

    private static readonly AuditAction Granted = AuditActions.RestrictionGranted;

    /// <inheritdoc/>
    public async ValueTask EditedAsync(
        string restriction,
        Restriction? before,
        Restriction? after,
        bool loosening,
        string? reason,
        SubjectId actor,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        var details = new Dictionary<string, JsonElement>(capacity: 5, StringComparer.Ordinal)
        {
            ["restriction"] = JsonSerializer.SerializeToElement(restriction),
            ["loosening"] = JsonSerializer.SerializeToElement(loosening),
            ["before"] = JsonSerializer.SerializeToElement(Written(before), Shape),
            ["after"] = JsonSerializer.SerializeToElement(Written(after), Shape),
        };

        if (reason is not null)
        {
            details["reason"] = JsonSerializer.SerializeToElement(reason);
        }

        await AppendAsync(Edited, actor, at, details, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask GrantedAsync(
        string restriction,
        int credit,
        string reason,
        SubjectId actor,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        await AppendAsync(
                Granted,
                actor,
                at,
                new Dictionary<string, JsonElement>(capacity: 3, StringComparer.Ordinal)
                {
                    ["restriction"] = JsonSerializer.SerializeToElement(restriction),
                    ["credit"] = JsonSerializer.SerializeToElement(credit),
                    ["reason"] = JsonSerializer.SerializeToElement(reason),
                },
                cancellationToken)
            .ConfigureAwait(false);

    // OPS-CFG-008 AC4: the values as the settings table writes them, so an entry
    // reads the same way as the row the edit produced.
    private static WrittenRestriction? Written(Restriction? restriction) =>
        restriction is null
            ? null
            : new WrittenRestriction(
                restriction.Key is RestrictionKeyKind.Host
                    ? HostPrefix + restriction.HostKeyName
                    : restriction.Key,
                restriction.Purpose,
                [.. restriction.Buckets.Select(bucket => new WrittenBucket(
                    bucket.Maximum,
                    XmlConvert.ToString(bucket.Interval),
                    bucket.Window))]);

    private async ValueTask AppendAsync(
        AuditAction action,
        SubjectId actor,
        DateTimeOffset at,
        Dictionary<string, JsonElement> details,
        CancellationToken cancellationToken) =>
        await records
            .AppendAsync(
                AuditRecord.Of(
                    AuditRecordId.New(time),
                    AuditCategory.Security,
                    action,
                    at,
                    actor,
                    actor,
                    organization: null,
                    details),
                cancellationToken)
            .ConfigureAwait(false);

    // The written form of one restriction in an audit entry, which is the form of
    // chapter 10 section 4.5: the key, the purpose and the buckets, and no more.
    private sealed record WrittenRestriction(
        object Key,
        RestrictionPurpose Purpose,
        IReadOnlyList<WrittenBucket> Buckets);

    private sealed record WrittenBucket(int Maximum, string Interval, BucketWindow Window);
}
