using System;
using Janus.Core;

namespace HostSample;

/// <summary>
/// Reads what the library answered, for code that goes on only where it succeeded.
/// </summary>
internal static class Outcomes
{
    /// <summary>
    /// The refusal an outcome carries.
    /// </summary>
    /// <param name="outcome">The outcome.</param>
    /// <returns>The refusal, or nothing where the outcome succeeded.</returns>
    public static Error? Refusal(this Result outcome) => outcome.Match<Error?>(() => null, error => error);

    /// <summary>
    /// The refusal an outcome carries.
    /// </summary>
    /// <typeparam name="TValue">What the outcome holds where it succeeded.</typeparam>
    /// <param name="outcome">The outcome.</param>
    /// <returns>The refusal, or nothing where the outcome succeeded.</returns>
    public static Error? Refusal<TValue>(this Result<TValue> outcome) => outcome.Match<Error?>(_ => null, error => error);

    /// <summary>
    /// The value of an outcome its caller has found to have succeeded.
    /// </summary>
    /// <typeparam name="TValue">What the outcome holds.</typeparam>
    /// <param name="outcome">The outcome.</param>
    /// <returns>The value.</returns>
    /// <exception cref="InvalidOperationException">The outcome is a refusal, which its caller did not look for.</exception>
    public static TValue Value<TValue>(this Result<TValue> outcome) =>
        outcome.Match(
            value => value,
            error => throw new InvalidOperationException("The outcome was read as a success and is the refusal " + error.Code + "."));
}
