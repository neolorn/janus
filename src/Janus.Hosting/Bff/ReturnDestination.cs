using System;

namespace Janus.Hosting.Bff;

/// <summary>
/// Where a route sends a browser whose request named its own destination: to that
/// destination where it is one path of this application, and to the root otherwise.
/// </summary>
/// <remarks>
/// Implements API-REDIR-001. A destination is one path of this application where it
/// begins with a solidus, its second character is neither a solidus nor a backslash,
/// and every character is printable ASCII (%x21 to %x7E) other than a backslash. The
/// whole value is judged and never its first characters alone: a browser removes every
/// tab and line break from an address before it reads it, so a destination holding one
/// between two solidi would be read as an address on another origin. The value judged
/// is the one the route read, decoded once, and one that is followed is followed as it
/// is, its own escapes, query and fragment untouched. Any other destination, and a
/// request naming none, is replaced and never refused, so nothing a browser is
/// answered tells the two apart.
/// </remarks>
internal static class ReturnDestination
{
    /// <summary>
    /// This application's root, which stands in for a destination that is not followed.
    /// </summary>
    public const string Root = "/";

    /// <summary>
    /// The destination a browser is sent to for the one its request named.
    /// </summary>
    /// <param name="returnTo">What the request named, or nothing where it named none.</param>
    /// <returns>What was named where it is one path of this application; the root otherwise.</returns>
    public static string Followed(string? returnTo) =>
        returnTo is not null && IsPath(returnTo) ? returnTo : Root;

    /// <summary>
    /// Whether the destination a request named is set aside for the root, which is
    /// what its route logs (API-REDIR-001 AC4).
    /// </summary>
    /// <param name="returnTo">What the request named, or nothing where it named none.</param>
    /// <returns>
    /// Whether a destination was named and is not followed. A request naming none, or
    /// an empty one, has nothing set aside.
    /// </returns>
    public static bool Replaced(string? returnTo) =>
        returnTo is { Length: > 0 } && !IsPath(returnTo);

    // API-REDIR-001: the one judgement of a destination, made on the whole value.
    private static bool IsPath(string returnTo) =>
        returnTo is { Length: > 0 }
        && returnTo[0] is '/'
        && (returnTo.Length is 1 || returnTo[1] is not '/')
        && !returnTo.AsSpan().ContainsAnyExceptInRange('!', '~')
        && !returnTo.Contains('\\', StringComparison.Ordinal);
}
