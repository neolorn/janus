using System;

namespace Janus.Authentication.Sending;

/// <summary>
/// What one scope has accumulated: the failures counted against it and when the last
/// of them was.
/// </summary>
/// <param name="Failures">How many were counted.</param>
/// <param name="At">When the last one was.</param>
/// <remarks>Implements AUTH-ABUSE-001.</remarks>
internal readonly record struct ThrottleCounter(int Failures, DateTimeOffset At);
