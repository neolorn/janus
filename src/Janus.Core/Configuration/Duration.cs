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
    /// Reads a duration as chapter 10 section 4 writes it.
    /// </summary>
    /// <param name="value">The duration, for example <c>P7Y</c>, <c>P30D</c>, <c>PT15M</c>.</param>
    /// <returns>The length of time the library holds for it.</returns>
    /// <exception cref="FormatException">The value is not a duration the section writes.</exception>
    public static TimeSpan Parse(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);

        if (value[0] != 'P')
        {
            throw Malformed(value);
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
                    throw Malformed(value);
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
                throw Malformed(value);
            }

            int quantity = int.Parse(value.AsSpan(digits, index - digits), CultureInfo.InvariantCulture);

            held += value[index] switch
            {
                'Y' when !afterT => TimeSpan.FromDays(quantity * DaysInAYear),
                'M' when !afterT => TimeSpan.FromDays(quantity * DaysInAMonth),
                'D' when !afterT => TimeSpan.FromDays(quantity),
                'H' when afterT => TimeSpan.FromHours(quantity),
                'M' when afterT => TimeSpan.FromMinutes(quantity),
                'S' when afterT => TimeSpan.FromSeconds(quantity),
                _ => throw Malformed(value),
            };

            anyComponent = true;
            index++;
        }

        return anyComponent ? held : throw Malformed(value);
    }

    private static FormatException Malformed(string value) =>
        new("'" + value + "' is not a duration chapter 10 section 4 writes.");
}
