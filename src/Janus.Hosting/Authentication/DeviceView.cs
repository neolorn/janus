using System;
using Janus.Core;

namespace Janus.Hosting.Authentication;

/// <summary>
/// One browser the account knows, as the account application lists it.
/// </summary>
/// <param name="Id">What the removing endpoint names it by.</param>
/// <param name="Kind">Whether it is trusted for the second step or merely remembered.</param>
/// <param name="Label">What it is called.</param>
/// <param name="CreatedAt">When it became known.</param>
/// <param name="LastUsedAt">When it was last seen.</param>
/// <remarks>
/// Implements AUTH-FACT-015, AUTH-FACT-016 and chapter 09 section 3. Sessions are not
/// listed here; they are their own list (AUTH-SESS-013).
/// </remarks>
internal sealed record DeviceView(
    string Id,
    DeviceKind Kind,
    string Label,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastUsedAt)
{
    /// <summary>
    /// Reads one browser.
    /// </summary>
    /// <param name="device">The browser.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The device is absent.</exception>
    public static DeviceView Of(DeviceSummary device)
    {
        ArgumentNullException.ThrowIfNull(device);

        return new DeviceView(
            device.Id.ToString(),
            device.Kind,
            device.Label.Value,
            device.CreatedAt,
            device.LastUsedAt);
    }
}
