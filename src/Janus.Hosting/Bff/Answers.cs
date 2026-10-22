using System;
using System.Text.Json;
using Janus.Core;
using Microsoft.AspNetCore.Http;

namespace Janus.Hosting.Bff;

/// <summary>
/// What an endpoint answers with, given what the contract returned: the library's own
/// endpoints and a host's alike.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-006, API-CONV-002, API-CONV-003 and BFF-HOST-003. An endpoint
/// is one line to a contract method and one line to say what success looks like; the
/// failure needs no line at all, because every failure answers the same way: the status
/// the code carries, its details and the request's correlation identifier. A code the
/// library does not list is a fault of the calling code and is answered as every fault
/// is, <c>system.fault</c> with nothing else. A refusal of a record of a type that
/// conceals its records is answered by the browser profile as the record's absence,
/// whatever the endpoint answers. A host's own codes, for its own business, are the
/// host's to answer.
/// </remarks>
public static class Answers
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
    /// <exception cref="ArgumentNullException">The failure is absent.</exception>
    public static IResult Refused(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new RefusedAnswer(error);
    }

    /// <summary>
    /// A failure carrying nothing but its code.
    /// </summary>
    /// <param name="code">What was refused.</param>
    /// <returns>The answer.</returns>
    internal static IResult Refused(ErrorCode code) => Refused(Error.From(code));

    /// <summary>
    /// A request the endpoint cannot read: a member it requires is absent, empty or not
    /// of the shape it takes.
    /// </summary>
    /// <param name="member">Which member.</param>
    /// <returns>The answer, naming the member and nothing of its value.</returns>
    /// <remarks>Implements API-CONV-002.</remarks>
    internal static IResult Malformed(string member) => Refused(
        Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement(member)));
}
