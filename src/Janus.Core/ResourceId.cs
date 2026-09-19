using System;

namespace Janus.Core;

/// <summary>
/// One of the host's own records, as the host names it. The library never reads the
/// record, so what the identifier means is the host's business and what it looks like
/// is its own.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GRANT-001 and CONV-DESIGN-004. It is text because a host's keys
/// are its own: an integer, a UUID or a code, all of which cross the boundary of
/// chapter 09 section 8 as a string.
/// </remarks>
public readonly record struct ResourceId
{
    private readonly string? _value;

    private ResourceId(string value) => _value = value;

    /// <summary>
    /// Reads the identifier of one of the host's records.
    /// </summary>
    /// <param name="value">The identifier, as the host renders it.</param>
    /// <returns>The identifier.</returns>
    /// <exception cref="ArgumentException">The value is absent or blank.</exception>
    public static ResourceId Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A resource identifier is not blank.", nameof(value));
        }

        return new ResourceId(value);
    }

    /// <summary>
    /// The identifier as it crosses the boundary.
    /// </summary>
    /// <returns>The identifier, or an empty string for an unset identifier.</returns>
    public override string ToString() => _value ?? string.Empty;
}
