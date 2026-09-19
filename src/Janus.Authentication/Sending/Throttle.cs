using System;

namespace Janus.Authentication.Sending;

/// <summary>
/// The progressive delay a failed attempt earns: nothing until the threshold, then a
/// delay that multiplies with each further failure, decays while none occurs, and
/// stops at a cap. No count disables anything.
/// </summary>
/// <remarks>
/// Implements AUTH-ABUSE-001. A fixed-count lockout is a denial-of-service weapon
/// usable by anyone who knows an address, at no cost to the attacker, which is why
/// the control is a delay and why the per-account component is capped.
/// </remarks>
internal static class Throttle
{
    /// <summary>
    /// How many failures still stand, the accumulated ones having halved once per
    /// half-life since the last of them.
    /// </summary>
    /// <param name="counter">What was counted, or nothing where none was.</param>
    /// <param name="now">The clock.</param>
    /// <param name="halfLife">How long the accumulation takes to halve.</param>
    /// <returns>The standing count.</returns>
    public static int Standing(ThrottleCounter? counter, DateTimeOffset now, TimeSpan halfLife)
    {
        if (counter is not ThrottleCounter counted || counted.Failures <= 0)
        {
            return 0;
        }

        if (halfLife <= TimeSpan.Zero)
        {
            return counted.Failures;
        }

        double halved = counted.Failures * Math.Pow(0.5, (now - counted.At) / halfLife);

        return halved < 1 ? 0 : (int)halved;
    }

    /// <summary>
    /// The delay a standing count earns.
    /// </summary>
    /// <param name="failures">The standing count.</param>
    /// <param name="terms">The threshold, the first delay and the multiplier.</param>
    /// <param name="cap">The most this component may reach.</param>
    /// <returns>How long the next attempt waits.</returns>
    /// <exception cref="ArgumentNullException">The terms are absent.</exception>
    public static TimeSpan Delay(int failures, ThrottleTerms terms, TimeSpan cap)
    {
        ArgumentNullException.ThrowIfNull(terms);

        if (!terms.Enabled || failures < terms.Threshold)
        {
            return TimeSpan.Zero;
        }

        double multiplied = terms.Initial.TotalSeconds
            * Math.Pow((double)terms.Factor, failures - terms.Threshold);

        return multiplied >= cap.TotalSeconds
            ? cap
            : TimeSpan.FromSeconds(multiplied);
    }

    /// <summary>
    /// The cap one scope answers to: the per-source ceiling for a source, and the
    /// smaller per-account cap for an account and for an identifier, which share it
    /// (AUTH-ABUSE-001).
    /// </summary>
    /// <param name="scope">Which scope.</param>
    /// <param name="terms">The configured caps.</param>
    /// <returns>The cap.</returns>
    /// <exception cref="ArgumentNullException">The terms are absent.</exception>
    public static TimeSpan Cap(ThrottleScope scope, ThrottleTerms terms)
    {
        ArgumentNullException.ThrowIfNull(terms);

        return scope is ThrottleScope.Source ? terms.Maximum : terms.AccountCap;
    }
}
