using System.Collections.Generic;

namespace Janus.Hosting.Alerting;

/// <summary>
/// Who one alert goes to on each channel. Destinations are lists, never single
/// values, so one person being unreachable is not the alerting being unreachable.
/// </summary>
/// <param name="Email">The addresses.</param>
/// <param name="Sms">The numbers.</param>
/// <remarks>Implements OPS-ALERT-004 and OPS-ALERT-004a.</remarks>
internal sealed record AlertAudience(IReadOnlyList<string> Email, IReadOnlyList<string> Sms);
