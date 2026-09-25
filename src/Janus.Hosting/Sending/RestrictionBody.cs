using System;
using System.Collections.Generic;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Hosting.Sending;

/// <summary>
/// What creating or replacing a restriction carries, as the request reads.
/// </summary>
/// <param name="Key">What it counts by: <c>destination</c>, <c>account</c>, <c>source</c>, <c>global</c> or <c>host:&lt;name&gt;</c>.</param>
/// <param name="Purpose">Which sends it counts; <c>any</c> where absent.</param>
/// <param name="Buckets">The limits it holds.</param>
/// <param name="Reason">Why, which a loosening requires.</param>
/// <remarks>Implements chapter 09 section 8, AUTH-ABUSE-004 and OPS-CFG-002.</remarks>
internal sealed record RestrictionBody(
    string? Key,
    string? Purpose,
    IReadOnlyList<BucketBody>? Buckets,
    string? Reason)
{
    /// <summary>
    /// The restriction the body describes under the name the path gives, or the member
    /// it cannot be read at.
    /// </summary>
    /// <param name="name">The restriction's name.</param>
    /// <returns>The restriction, or nothing and the member that stopped it.</returns>
    public (Restriction? Restriction, string Member) Read(string name)
    {
        if (!Keyed(Key, out RestrictionKeyKind kind, out string? host))
        {
            return (null, "key");
        }

        RestrictionPurpose purpose = RestrictionPurpose.Any;

        if (Purpose is string written && !SettingText.TryRead(written, out purpose))
        {
            return (null, "purpose");
        }

        if (Buckets is null)
        {
            return (null, "buckets");
        }

        var buckets = new List<Bucket>(Buckets.Count);

        foreach (BucketBody? bucket in Buckets)
        {
            if (bucket?.Read() is not Bucket read)
            {
                return (null, "buckets");
            }

            buckets.Add(read);
        }

        return (new Restriction(name, kind, host, purpose, buckets), string.Empty);
    }

    private static bool Keyed(string? written, out RestrictionKeyKind kind, out string? host)
    {
        host = null;

        if (written is null)
        {
            kind = default;
            return false;
        }

        if (written.StartsWith(RestrictionView.HostPrefix, StringComparison.Ordinal))
        {
            kind = RestrictionKeyKind.Host;
            host = written[RestrictionView.HostPrefix.Length..];
            return host.Length > 0;
        }

        return SettingText.TryRead(written, out kind) && kind is not RestrictionKeyKind.Host;
    }
}
