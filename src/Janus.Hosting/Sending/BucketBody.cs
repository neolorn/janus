using System;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Hosting.Sending;

/// <summary>
/// One limit of a restriction, as the request reads.
/// </summary>
/// <param name="Max">How many sends the interval admits.</param>
/// <param name="Interval">The interval, as an ISO 8601 duration.</param>
/// <param name="Window"><c>sliding</c> or <c>fixed</c>.</param>
/// <remarks>Implements chapter 09 section 8 and AUTH-ABUSE-004.</remarks>
internal sealed record BucketBody(int? Max, string? Interval, string? Window)
{
    /// <summary>
    /// The bucket the body describes, or nothing where it cannot be read as one.
    /// </summary>
    /// <returns>The bucket, or nothing.</returns>
    public Bucket? Read() =>
        Max is int max
        && max >= 0
        && Interval is string written
        && Duration.TryParse(written, out TimeSpan interval)
        && interval > TimeSpan.Zero
        && Window is string window
        && SettingText.TryRead(window, out BucketWindow kind)
            ? new Bucket(max, interval, kind)
            : null;
}
