using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Janus.Core;

namespace Janus.Privacy.Erasures;

/// <summary>
/// One line of the off-host erasure ledger: when, whose and why.
/// </summary>
/// <param name="ErasedAt">The instant of the erasure, to the second.</param>
/// <param name="Subject">Whose erasure it was.</param>
/// <param name="Reason">Why it happened.</param>
/// <remarks>
/// Implements DR-016 and chapter 10 section 5.12a. The subject identifier is derived
/// from nothing about the person and is the only thing of theirs the line holds: no
/// name, address, email, phone or fingerprint (DR-016 AC4). A line is read only in the
/// one form it is written in, so a ledger that was edited by hand is refused rather
/// than half understood.
/// </remarks>
internal sealed record ErasureLedgerLine(DateTimeOffset ErasedAt, SubjectId Subject, ErasureReason Reason)
{
    private const string Instant = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'";

    private const char Separator = ' ';

    // Chapter 10 section 5.12a: the reason is written in the spelling the chapter gives
    // it, which is the name on the member.
    private static readonly JsonSerializerOptions Spelled =
        new() { Converters = { new JsonStringEnumConverter() } };

    /// <summary>
    /// The line an erasure's delivery is written down as.
    /// </summary>
    /// <param name="raised">The erasure, as its delivery raises it.</param>
    /// <returns>The line.</returns>
    /// <exception cref="ArgumentNullException">The erasure is absent.</exception>
    /// <exception cref="InvalidOperationException">The erasure names no subject.</exception>
    public static ErasureLedgerLine Of(ErasureRequested raised)
    {
        ArgumentNullException.ThrowIfNull(raised);

        SubjectId subject = raised.Subject
            ?? throw new InvalidOperationException("An erasure is always about a subject.");

        return new ErasureLedgerLine(
            raised.RaisedAt.AddTicks(-(raised.RaisedAt.UtcTicks % TimeSpan.TicksPerSecond)),
            subject,
            raised.Reason);
    }

    /// <summary>
    /// Reads one line of a ledger.
    /// </summary>
    /// <param name="line">The line, without its terminator.</param>
    /// <returns>The line read, or nothing where it is not in the form a line is written in.</returns>
    /// <exception cref="ArgumentNullException">The line is absent.</exception>
    public static ErasureLedgerLine? Read(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        string[] fields = line.Split(Separator);

        if (fields.Length != 3
            || !DateTimeOffset.TryParseExact(
                fields[0],
                Instant,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset erasedAt)
            || !Guid.TryParseExact(fields[1], "D", out Guid subject)
            || Enum.GetValues<ErasureReason>().Where(reason => Spelling(reason) == fields[2]).ToArray()
                is not [ErasureReason reason])
        {
            return null;
        }

        var read = new ErasureLedgerLine(erasedAt, new SubjectId(subject), reason);

        // The one written form: a subject in capitals or an offset other than Z reads
        // as the same erasure, but is not a line the library wrote.
        return string.Equals(read.Written(), line, StringComparison.Ordinal) ? read : null;
    }

    /// <summary>
    /// The line as the ledger holds it.
    /// </summary>
    /// <returns>The instant, the subject identifier and the reason, one space apart.</returns>
    public string Written() =>
        string.Join(
            Separator,
            ErasedAt.UtcDateTime.ToString(Instant, CultureInfo.InvariantCulture),
            Subject.ToString(),
            Spelling(Reason));

    private static string Spelling(ErasureReason reason) =>
        JsonSerializer.SerializeToElement(reason, Spelled).GetString()!;
}
