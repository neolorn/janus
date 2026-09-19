using System;
using Janus.Core;

namespace Janus.Authorization.Tests;

/// <summary>
/// Reads the value or the code out of an outcome, so that a test asserting on one of
/// them says so in one line and fails loudly when it got the other.
/// </summary>
internal static class Outcome
{
    /// <summary>
    /// The value an outcome that succeeded carries.
    /// </summary>
    /// <typeparam name="TValue">What the operation produces.</typeparam>
    /// <param name="result">The outcome.</param>
    /// <returns>The value.</returns>
    /// <exception cref="InvalidOperationException">The outcome was a failure.</exception>
    public static TValue Value<TValue>(Result<TValue> result) =>
        result.Match(
            value => value,
            failure => throw new InvalidOperationException($"Expected a value, got {failure.Code}."));

    /// <summary>
    /// The code an outcome that failed carries.
    /// </summary>
    /// <typeparam name="TValue">What the operation produces when it succeeds.</typeparam>
    /// <param name="result">The outcome.</param>
    /// <returns>The code.</returns>
    /// <exception cref="InvalidOperationException">The outcome succeeded.</exception>
    public static ErrorCode Code<TValue>(Result<TValue> result) =>
        result.Match(
            _ => throw new InvalidOperationException("Expected a failure, got a value."),
            failure => failure.Code);

    /// <summary>
    /// The code an outcome that failed carries.
    /// </summary>
    /// <param name="result">The outcome.</param>
    /// <returns>The code.</returns>
    /// <exception cref="InvalidOperationException">The outcome succeeded.</exception>
    public static ErrorCode Code(Result result) =>
        result.Match(
            () => throw new InvalidOperationException("Expected a failure, got success."),
            failure => failure.Code);
}
