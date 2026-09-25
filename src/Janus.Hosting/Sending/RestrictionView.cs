using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Hosting.Sending;

/// <summary>
/// One restriction as the administration interface answers it.
/// </summary>
/// <param name="Name">The restriction's name.</param>
/// <param name="Key">What it counts by: <c>destination</c>, <c>account</c>, <c>source</c>, <c>global</c> or <c>host:&lt;name&gt;</c>.</param>
/// <param name="Purpose">Which sends it counts.</param>
/// <param name="Buckets">The limits it holds.</param>
/// <remarks>Implements chapter 09 section 8 and AUTH-ABUSE-004.</remarks>
internal sealed record RestrictionView(
    string Name,
    string Key,
    string Purpose,
    IReadOnlyList<BucketView> Buckets)
{
    /// <summary>
    /// The prefix a key a host supplies is written with.
    /// </summary>
    public const string HostPrefix = "host:";

    /// <summary>
    /// The view of one restriction.
    /// </summary>
    /// <param name="restriction">The restriction as the contract reads it.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The restriction is absent.</exception>
    public static RestrictionView Of(Restriction restriction)
    {
        ArgumentNullException.ThrowIfNull(restriction);

        return new RestrictionView(
            restriction.Name,
            restriction.Key is RestrictionKeyKind.Host
                ? HostPrefix + restriction.HostKeyName
                : SettingText.Of(restriction.Key),
            SettingText.Of(restriction.Purpose),
            [.. restriction.Buckets.Select(BucketView.Of)]);
    }
}
