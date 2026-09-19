using System;
using System.Globalization;

namespace Janus.Core.Configuration;

/// <summary>
/// The ISO 8601 durations chapter 10 section 4 writes. A year is held at 366 days and
/// a month at 31 days, so a value written in either is never shorter than any calendar
/// span of that length and a floor stated in years cannot be undercut by a leap day.
/// </summary>
/// <remarks>
/// Implements the chapter 10 section 4 preamble and D-152. The framework's duration
/// type carries no calendar, which is the reason the holding rule exists. The grammar
/// is the section's own, so a unit the section never writes is not accepted.
/// </remarks>
internal static class Duration
{
    private const int DaysInAYear = 366;
    private const int DaysInAMonth = 31;

    /// <summary>
    /// Reads a duration where a text that is not one is an outcome rather than a
    /// fault: the settings table holds text a tightened key may no longer write.
    /// </summary>
    /// <param name="value">The duration, for example <c>P7Y</c>, <c>P30D</c>, <c>PT15M</c>.</param>
    /// <param name="duration">The length of time, where the text is a duration.</param>
    /// <returns>Whether the text is a duration the section writes.</returns>
    public static bool TryParse(string value, out TimeSpan duration)
    {
        duration = TimeSpan.Zero;

        if (string.IsNullOrEmpty(value) || value[0] != 'P')
        {
            return false;
        }

        TimeSpan held = TimeSpan.Zero;
        bool afterT = false;
        bool anyComponent = false;
        int index = 1;

        while (index < value.Length)
        {
            if (value[index] == 'T')
            {
                if (afterT)
                {
                    return false;
                }

                afterT = true;
                index++;
                continue;
            }

            int digits = index;

            while (index < value.Length && char.IsAsciiDigit(value[index]))
            {
                index++;
            }

            if (index == digits || index == value.Length)
            {
                return false;
            }

            int quantity = int.Parse(value.AsSpan(digits, index - digits), CultureInfo.InvariantCulture);

            switch (value[index])
            {
                case 'Y' when !afterT:
                    held += TimeSpan.FromDays(quantity * DaysInAYear);
                    break;
                case 'M' when !afterT:
                    held += TimeSpan.FromDays(quantity * DaysInAMonth);
                    break;
                case 'D' when !afterT:
                    held += TimeSpan.FromDays(quantity);
                    break;
                case 'H' when afterT:
                    held += TimeSpan.FromHours(quantity);
                    break;
                case 'M' when afterT:
                    held += TimeSpan.FromMinutes(quantity);
                    break;
                case 'S' when afterT:
                    held += TimeSpan.FromSeconds(quantity);
                    break;
                default:
                    return false;
            }

            anyComponent = true;
            index++;
        }

        duration = held;
        return anyComponent;
    }

    /// <summary>
    /// Reads a duration as chapter 10 section 4 writes it.
    /// </summary>
    /// <param name="value">The duration, for example <c>P7Y</c>, <c>P30D</c>, <c>PT15M</c>.</param>
    /// <returns>The length of time the library holds for it.</returns>
    /// <exception cref="FormatException">The value is not a duration the section writes.</exception>
    public static TimeSpan Parse(string value) => TryParse(value, out TimeSpan duration)
        ? duration
        : throw new FormatException("'" + value + "' is not a duration chapter 10 section 4 writes.");
}
