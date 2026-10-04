using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Hosting.Sending;

/// <summary>
/// One file of datacenter ranges, read whole and held as ranges ordered by their first
/// address.
/// </summary>
/// <remarks>
/// Implements AUTH-ABUSE-008 over the format <see cref="IDatacenterRangeSource"/>
/// describes, which is the location file's reduced to its ranges (INT-GEN-006). A file
/// is taken whole or not at all: one line that cannot be read, a range of mixed family
/// or reversed, a range that overlaps another or a file with no date refuses it, because
/// a file read in part would match some addresses and silently not others.
/// </remarks>
internal sealed class DatacenterRangeFile
{
    private const char Marker = '#';
    private const char Separator = '\t';
    private const int Fields = 2;

    private readonly UInt128[] _firsts;
    private readonly UInt128[] _lasts;

    private DatacenterRangeFile(DateOnly produced, UInt128[] firsts, UInt128[] lasts)
    {
        Produced = produced;
        _firsts = firsts;
        _lasts = lasts;
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
    public static async ValueTask<DatacenterRangeFile?> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);

        DateOnly? produced = null;
        List<(UInt128 First, UInt128 Last)> ranges = [];

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

            ranges.Add(range);
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

        return new DatacenterRangeFile(
            dated,
            [.. ranges.Select(range => range.First)],
            [.. ranges.Select(range => range.Last)]);
    }

    /// <summary>
    /// Whether an address falls in a range the file lists.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <returns>Whether a range holds the address.</returns>
    /// <exception cref="ArgumentNullException">The address is absent.</exception>
    public bool Contains(IPAddress address)
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

        return at >= 0 && wanted <= _lasts[at];
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

    private static (UInt128 First, UInt128 Last)? Range(string line)
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

        return (Number(first), Number(last));
    }
}
