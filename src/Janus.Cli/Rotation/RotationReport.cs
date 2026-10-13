using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Janus.Privacy.SubjectKeys;

namespace Janus.Cli.Rotation;

/// <summary>
/// What a rotation command prints on standard output: the version and the count, and at
/// a retirement the versions retired and the date they are kept until; nothing of a key.
/// </summary>
/// <remarks>
/// Implements OPS-SEC-003 and D-166 (317). A retired version leaves the
/// application's key document at once and stays in the envelope and the secrets manager
/// until every backup taken under it has expired, as a previous backup key does (D-103),
/// which is the rotation's completion and the backup retention.
/// </remarks>
internal static class RotationReport
{
    /// <summary>
    /// The report of a pass: the version every value is now under and the count.
    /// </summary>
    /// <param name="progress">The rotation.</param>
    /// <returns>The report.</returns>
    public static string Of(KeyRotationProgress progress) =>
        Written(new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["version"] = progress.Version,
            ["processed"] = progress.Processed,
        });

    /// <summary>
    /// The report of a retirement: the pass's, with the versions retired and the date
    /// they are kept until.
    /// </summary>
    /// <param name="retirement">The retirement.</param>
    /// <param name="retention">How long a backup is kept.</param>
    /// <returns>The report.</returns>
    /// <exception cref="InvalidOperationException">The rotation retired has not completed.</exception>
    public static string Of(KeyRetirement retirement, TimeSpan retention)
    {
        DateTimeOffset completed = retirement.Rotation.CompletedAt
            ?? throw new InvalidOperationException("A rotation retires only once it has completed.");

        return Written(new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["version"] = retirement.Rotation.Version,
            ["processed"] = retirement.Rotation.Processed,
            ["retired"] = retirement.Retired,
            ["keepUntil"] = (completed + retention).UtcDateTime,
        });
    }

    private static string Written(Dictionary<string, object> report) =>
        Encoding.UTF8.GetString(JsonSerializer.SerializeToUtf8Bytes(report));
}
