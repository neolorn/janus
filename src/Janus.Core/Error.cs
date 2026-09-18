using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Janus.Core;

/// <summary>
/// The failure an expected outcome carries: a code from the catalogue and the
/// structured context that belongs to it. Never a sentence a person reads.
/// </summary>
/// <remarks>Implements CONV-DESIGN-005, LIB-API-003, API-CONV-002.</remarks>
[SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "CONV-DESIGN-005 names the failure an Error, and the code carries the word the specification uses.")]
public sealed record Error
{
    private static readonly IReadOnlyDictionary<string, JsonElement> NoDetails =
        new Dictionary<string, JsonElement>(capacity: 0);

    /// <summary>
    /// A failure carrying a code and its structured context.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <param name="details">Structured context, never a sentence.</param>
    /// <exception cref="ArgumentException">The code is unset.</exception>
    /// <exception cref="ArgumentNullException">The details are absent.</exception>
    public Error(ErrorCode code, IReadOnlyDictionary<string, JsonElement> details)
    {
        if (code == default)
        {
            throw new ArgumentException("A failure carries a code from the catalogue.", nameof(code));
        }

        ArgumentNullException.ThrowIfNull(details);

        Code = code;
        Details = details;
    }

    /// <summary>
    /// The code, stable across rewordings of any message a host renders from it.
    /// </summary>
    public ErrorCode Code { get; }

    /// <summary>
    /// Structured context, serialized as the details object of the error envelope.
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement> Details { get; }

    /// <summary>
    /// A failure that carries no context beyond its code.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <returns>The failure.</returns>
    public static Error From(ErrorCode code) => new(code, NoDetails);

    /// <summary>
    /// A failure carrying one named value.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <param name="name">The name the value appears under in the details object.</param>
    /// <param name="value">The value.</param>
    /// <returns>The failure.</returns>
    public static Error From(ErrorCode code, string name, JsonElement value) =>
        new(code, new Dictionary<string, JsonElement>(capacity: 1) { [name] = value });
}
