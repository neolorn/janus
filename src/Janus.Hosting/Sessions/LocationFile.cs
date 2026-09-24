using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sessions;
using Janus.Core;

namespace Janus.Hosting.Sessions;

/// <summary>
/// One IP-to-city database file, read whole and held as ranges ordered by their first
/// address.
/// </summary>
/// <remarks>
/// Implements INT-GEN-006 and AUTH-SESS-013 over the format <see cref="ILocationSource"/>
/// describes. A file is taken whole or not at all: one line that cannot be read, a
/// range that overlaps another or a file with no date refuses it, because a file read
/// in part would resolve some addresses and silently not others.
/// </remarks>
internal sealed class LocationFile
{
    private const char Marker = '#';
    private const char Separator = '\t';
    private const int Fields = 6;

    private readonly UInt128[] _firsts;
    private readonly UInt128[] _lasts;
    private readonly ResolvedLocation?[] _places;

    private LocationFile(DateOnly produced, UInt128[] firsts, UInt128[] lasts, ResolvedLocation?[] places)
    {
        Produced = produced;
        _firsts = firsts;
        _lasts = lasts;
        _places = places;
    }

    /// <summary>
    /// The date the data was produced, which its age is judged from.
    /// </summary>
    public DateOnly Produced { get; }

    /// <summary>
    /// Reads a file to its end.
    /// </summary>
    /// <param name="stream">The file.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The file, or nothing where it is refused.</returns>
    /// <exception cref="ArgumentNullException">The stream is absent.</exception>
    public static async ValueTask<LocationFile?> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);

        DateOnly? produced = null;
        List<(UInt128 First, UInt128 Last, ResolvedLocation? Place)> ranges = [];
        Dictionary<ResolvedLocation, ResolvedLocation> places = [];

        using StreamReader reading = new(stream, leaveOpen: true);

        while (await reading.ReadLineAsync(cancellationToken).ConfigureAwait(false) is string line)
        {
            if (line.Length is 0)
            {
                continue;
            }

            if (line[0] is Marker)
            {
                produced ??= Dated(line);

                continue;
            }

            if (Range(line) is not { } range)
            {
                return null;
            }

            ResolvedLocation? place = range.Place is null
                ? null
                : places.TryGetValue(range.Place, out ResolvedLocation? held) ? held : places[range.Place] = range.Place;

            ranges.Add((range.First, range.Last, place));
        }

        if (produced is not DateOnly dated)
        {
            return null;
        }

        ranges.Sort((one, other) => one.First.CompareTo(other.First));

        for (int at = 1; at < ranges.Count; at++)
        {
            if (ranges[at].First <= ranges[at - 1].Last)
            {
                return null;
            }
        }

        return new LocationFile(
            dated,
            [.. ranges.Select(range => range.First)],
            [.. ranges.Select(range => range.Last)],
            [.. ranges.Select(range => range.Place)]);
    }

    /// <summary>
    /// Where an address is, no finer than a city.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <returns>The place, or nothing where no range holds the address or its range names none.</returns>
    /// <exception cref="ArgumentNullException">The address is absent.</exception>
    public ResolvedLocation? Find(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        UInt128 wanted = Number(address);
        int at = Array.BinarySearch(_firsts, wanted);

        // No range begins at the address itself, so the one that might hold it is the
        // last to begin before it.
        if (at < 0)
        {
            at = ~at - 1;
        }

        return at >= 0 && wanted <= _lasts[at] ? _places[at] : null;
    }

    // Every address is held in the IPv6 space, an IPv4 address as its mapped form, so
    // one ordering answers both families and a mapped address finds its IPv4 range.
    private static UInt128 Number(IPAddress address) =>
        BinaryPrimitives.ReadUInt128BigEndian(address.MapToIPv6().GetAddressBytes());

    private static DateOnly? Dated(string line) =>
        DateOnly.TryParseExact(
            line[1..].Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out DateOnly produced)
            ? produced
            : null;

    private static (UInt128 First, UInt128 Last, ResolvedLocation? Place)? Range(string line)
    {
        string[] fields = line.Split(Separator);

        if (fields.Length != Fields
            || !IPAddress.TryParse(fields[0], out IPAddress? first)
            || !IPAddress.TryParse(fields[1], out IPAddress? last)
            || first.AddressFamily != last.AddressFamily
            || Number(first) > Number(last))
        {
            return null;
        }

        string country = fields[2];
        string city = fields[3];

        if (country.Length is not (0 or 2) || !country.All(char.IsAsciiLetter))
        {
            return null;
        }

        if (city.Length is 0)
        {
            if (fields[4].Length is not 0 || fields[5].Length is not 0)
            {
                return null;
            }

            return (Number(first), Number(last), country.Length is 0
                ? null
                : new ResolvedLocation(new SessionLocation(City: null, country.ToUpperInvariant()), Coordinates: null));
        }

        if (city.Trim().Length != city.Length
            || !Degrees(fields[4], 90, out double latitude)
            || !Degrees(fields[5], 180, out double longitude))
        {
            return null;
        }

        return (
            Number(first),
            Number(last),
            new ResolvedLocation(
                new SessionLocation(city, country.Length is 0 ? null : country.ToUpperInvariant()),
                new Coordinates(latitude, longitude)));
    }

    private static bool Degrees(string field, double bound, out double degrees) =>
        double.TryParse(field, NumberStyles.Float, CultureInfo.InvariantCulture, out degrees)
        && double.IsFinite(degrees)
        && Math.Abs(degrees) <= bound;
}
