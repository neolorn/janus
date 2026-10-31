using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Janus.Privacy.SubjectKeys;

namespace Janus.Cli.Rotation;

/// <summary>
/// What a rotation command prints on standard output: the version and the count, and at
/// a retirement the versions retired; nothing of a key.
/// </summary>
/// <remarks>
/// Implements OPS-SEC-003 AC4 (D-225). A retired version leaves the application's key
/// document at once; keeping it for a backup taken under it is the deployment's.
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
    /// The report of a retirement: the pass's, with the versions retired.
    /// </summary>
    /// <param name="retirement">The retirement.</param>
    /// <returns>The report.</returns>
    public static string Of(KeyRetirement retirement) =>
        Written(new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["version"] = retirement.Rotation.Version,
            ["processed"] = retirement.Rotation.Processed,
            ["retired"] = retirement.Retired,
        });

    private static string Written(Dictionary<string, object> report) =>
        Encoding.UTF8.GetString(JsonSerializer.SerializeToUtf8Bytes(report));
}
