using System;
using System.Xml;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Hosting.Sending;

/// <summary>
/// One limit of a restriction as the administration interface answers it.
/// </summary>
/// <param name="Max">How many sends the interval admits.</param>
/// <param name="Interval">The interval, as an ISO 8601 duration.</param>
/// <param name="Window">Whether the interval slides or is fixed.</param>
/// <remarks>Implements chapter 09 section 8 and AUTH-ABUSE-004.</remarks>
internal sealed record BucketView(int Max, string Interval, string Window)
{
    /// <summary>
    /// The view of one bucket.
    /// </summary>
    /// <param name="bucket">The bucket as the contract reads it.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The bucket is absent.</exception>
    public static BucketView Of(Bucket bucket)
    {
        ArgumentNullException.ThrowIfNull(bucket);

        return new BucketView(bucket.Maximum, XmlConvert.ToString(bucket.Interval), SettingText.Of(bucket.Window));
    }
}
