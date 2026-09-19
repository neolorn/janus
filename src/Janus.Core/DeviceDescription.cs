using System;

namespace Janus.Core;

/// <summary>
/// What a session or a credential records about where it was used, derived server
/// side from the request and rendered by the frontend. Nothing else is read from the
/// header it comes from.
/// </summary>
/// <param name="Browser">The browser, at most sixty-four characters.</param>
/// <param name="Os">The operating system, at most sixty-four characters.</param>
/// <remarks>Implements AUTH-FACT-001 and AUTH-SESS-013.</remarks>
public sealed record DeviceDescription(string Browser, string Os)
{
    /// <summary>
    /// The longest either part may be.
    /// </summary>
    public const int MaximumLength = 64;

    /// <summary>
    /// The browser, at most sixty-four characters.
    /// </summary>
    /// <exception cref="ArgumentException">The part is absent or too long.</exception>
    public string Browser { get; init => field = Part(value); } = Part(Browser);

    /// <summary>
    /// The operating system, at most sixty-four characters.
    /// </summary>
    /// <exception cref="ArgumentException">The part is absent or too long.</exception>
    public string Os { get; init => field = Part(value); } = Part(Os);

    /// <summary>
    /// The default label a credential takes where the person supplies none.
    /// </summary>
    /// <returns>The two parts joined by a space.</returns>
    public override string ToString() => Browser + " " + Os;

    private static string Part(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        return value.Length <= MaximumLength
            ? value
            : throw new ArgumentException(
                "A device description is at most " + MaximumLength + " characters a part.",
                nameof(value));
    }
}
