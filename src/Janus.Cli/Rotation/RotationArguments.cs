using System;
using System.Collections.Generic;
using System.Text.Json;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Cli.Rotation;

/// <summary>
/// Reads what <c>rotate-kek</c> and <c>rotate-fingerprint-key</c> are given: nothing, to
/// rotate; or <c>--sealed</c>, to confirm the escrow copy sealed, with
/// <c>--retention duration</c> where the deployment keeps its backups longer than
/// <c>backup.retention</c>'s default.
/// </summary>
/// <remarks>
/// Implements OPS-SEC-003 and D-166 (317). The commands run without the
/// application, so the retention they report from is the default unless the operator
/// names one, and a retention shorter than the default is read as the default: the date
/// a retired version is kept until is never earlier than the default gives.
/// </remarks>
internal static class RotationArguments
{
    /// <summary>
    /// The argument that confirms the escrow copy sealed.
    /// </summary>
    public const string Sealed = "--sealed";

    /// <summary>
    /// The argument that names the deployment's backup retention, as an ISO 8601
    /// duration.
    /// </summary>
    public const string Retention = "--retention";

    /// <summary>
    /// Reads the arguments that follow the command's name.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <returns>
    /// What they ask for, or the failure naming the argument that is unknown, repeated,
    /// without its value, not a duration, or a retention given without the seal.
    /// </returns>
    /// <exception cref="ArgumentNullException">The arguments are absent.</exception>
    public static Result<RotationRequest> Read(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        bool sealedCopy = false;
        TimeSpan? named = null;

        for (int at = 0; at < arguments.Count; at++)
        {
            string argument = arguments[at];

            if (argument == Sealed && !sealedCopy)
            {
                sealedCopy = true;
            }
            else if (argument == Retention
                && named is null
                && at + 1 < arguments.Count
                && Duration.TryParse(arguments[at + 1], out TimeSpan retention))
            {
                named = retention;
                at++;
            }
            else
            {
                return Result.Failure<RotationRequest>(Malformed(argument));
            }
        }

        if (named is not null && !sealedCopy)
        {
            return Result.Failure<RotationRequest>(Malformed(Retention));
        }

        TimeSpan fallback = Settings.BackupRetention.Default;

        return Result.Success(new RotationRequest(
            sealedCopy,
            named is TimeSpan longer && longer > fallback ? longer : fallback));
    }

    private static Error Malformed(string member) =>
        Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement(member));
}
