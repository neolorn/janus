using System;
using Janus.Core;
using Microsoft.AspNetCore.Http;

namespace Janus.Hosting.Bff;

/// <summary>
/// What an endpoint answers with, given what the contract returned.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-006, API-CONV-002 and API-CONV-003. An endpoint is one
/// line to a contract method and one line to say what success looks like; the
/// failure needs no line at all, because every failure answers the same way.
/// </remarks>
internal static class Answers
{
    /// <summary>
    /// The success, or the failure the code decides the status of.
    /// </summary>
    /// <typeparam name="TValue">What the operation returned.</typeparam>
    /// <param name="outcome">The outcome.</param>
    /// <param name="answered">What success answers with.</param>
    /// <returns>The answer.</returns>
    /// <exception cref="ArgumentNullException">The success is absent.</exception>
    public static IResult Of<TValue>(Result<TValue> outcome, Func<TValue, IResult> answered)
    {
        ArgumentNullException.ThrowIfNull(answered);

        return outcome.Match(answered, Refused);
    }

    /// <summary>
    /// The success, or the failure the code decides the status of.
    /// </summary>
    /// <param name="outcome">The outcome.</param>
    /// <param name="answered">What success answers with.</param>
    /// <returns>The answer.</returns>
    /// <exception cref="ArgumentNullException">The success is absent.</exception>
    public static IResult Of(Result outcome, IResult answered)
    {
        ArgumentNullException.ThrowIfNull(answered);

        return outcome.Match(() => answered, Refused);
    }

    /// <summary>
    /// A failure, whatever produced it.
    /// </summary>
    /// <param name="error">What was refused.</param>
    /// <returns>The answer.</returns>
    public static IResult Refused(Error error) => new RefusedAnswer(error);

    /// <summary>
    /// A failure carrying nothing but its code.
    /// </summary>
    /// <param name="code">What was refused.</param>
    /// <returns>The answer.</returns>
    public static IResult Refused(ErrorCode code) => Refused(Error.From(code));
}
