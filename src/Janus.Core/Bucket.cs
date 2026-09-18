using System;

namespace Janus.Core;

/// <summary>
/// One allowance a sending restriction counts against: how many sends, over what
/// interval, and how the interval is counted.
/// </summary>
/// <param name="Maximum">The sends the interval admits.</param>
/// <param name="Interval">The interval.</param>
/// <param name="Window">How the interval is counted.</param>
/// <remarks>Implements chapter 10 section 5.16, AUTH-ABUSE-004.</remarks>
public sealed record Bucket(int Maximum, TimeSpan Interval, BucketWindow Window);
