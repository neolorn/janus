using System;
using System.Collections.Generic;
using System.Text.Json;
using Janus.Core;

namespace Janus.Cli.Rotation;

/// <summary>
/// Reads what <c>rotate-kek</c> and <c>rotate-fingerprint-key</c> are given: nothing, to
/// rotate; or <c>--retire</c>, to retire the versions before a completed rotation's.
/// </summary>
/// <remarks>
/// Implements OPS-SEC-003 AC4 (D-225).
/// </remarks>
internal static class RotationArguments
{
    /// <summary>
    /// The argument that retires the versions before a completed rotation's.
    /// </summary>
    public const string Retire = "--retire";

    /// <summary>
    /// The arguments both commands take (LIB-API-001): none to rotate, or the retirement.
    /// </summary>
    public static IReadOnlyList<CommandArgument> Declared { get; } =
    [
        new(Retire, Required: false, "flag"),
    ];

    /// <summary>
    /// Reads the arguments that follow the command's name.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <returns>
    /// Whether the retirement is asked for, or the failure naming the argument that is
    /// unknown or repeated.
    /// </returns>
    /// <exception cref="ArgumentNullException">The arguments are absent.</exception>
    public static Result<bool> Read(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        bool retire = false;

        foreach (string argument in arguments)
        {
            if (argument != Retire || retire)
            {
                return Result.Failure<bool>(Malformed(argument));
            }

            retire = true;
        }

        return Result.Success(retire);
    }

    private static Error Malformed(string member) =>
        Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement(member));
}
