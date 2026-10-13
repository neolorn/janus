using System;
using System.Text.Json;
using Janus.Core;

namespace Janus.Hosting.Bff;

/// <summary>
/// Where a navigation route returns the browser it refused: back to where it started,
/// carrying the code of the refusal.
/// </summary>
/// <remarks>
/// Implements BFF-ERR-001, BFF-ABUSE-001 and CONV-CONTENT-001. The code crosses in the
/// query member <c>error</c>, placed before any fragment, and the frontend says what
/// it means. A refusal naming the instant its wait lifts, as a throttled refusal and a
/// send a restriction refuses do, carries that instant beside the code, and nothing
/// else of the refusal's details crosses.
/// </remarks>
internal static class NavigationReturn
{
    private const string RetryAt = "retryAt";

    /// <summary>
    /// The address a refused browser is returned to.
    /// </summary>
    /// <param name="destination">Where on this application the browser started from.</param>
    /// <param name="refusal">What refused it.</param>
    /// <returns>The destination, carrying the refusal's code.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static string Refused(string destination, Error refusal)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(refusal);

        int fragment = destination.IndexOf('#', StringComparison.Ordinal);
        string path = fragment < 0 ? destination : destination[..fragment];
        string rest = fragment < 0 ? string.Empty : destination[fragment..];
        char separator = path.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        string query = "error=" + Uri.EscapeDataString(refusal.Code.ToString());

        if (refusal.Details.TryGetValue(RetryAt, out JsonElement retryAt)
            && retryAt.GetString() is { } instant)
        {
            query += "&" + RetryAt + "=" + Uri.EscapeDataString(instant);
        }

        return path + separator + query + rest;
    }
}
