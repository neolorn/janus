using System;
using Janus.Authentication.Sending;
using Janus.Core;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// Where the links a test deployment sends land, and the token a link carries.
/// </summary>
internal static class Landing
{
    /// <summary>
    /// The two applications a link lands on.
    /// </summary>
    public static LandingOrigins Origins { get; } = new(
        "https://accounts.example.test",
        "https://account.example.test");

    /// <summary>
    /// The links composed under those origins.
    /// </summary>
    public static LandingLinks Links { get; } = new(Origins);

    /// <summary>
    /// The token a link carries, which follows its kind in the fragment; a kind holds no
    /// full stop and a token none either, so the last one parts them.
    /// </summary>
    /// <param name="link">The address the message carried.</param>
    /// <returns>The token.</returns>
    public static string Token(string link)
    {
        ArgumentNullException.ThrowIfNull(link);

        return link[(link.LastIndexOf('.') + 1)..];
    }

    /// <summary>
    /// The token the link a message carried holds.
    /// </summary>
    /// <param name="sent">The message.</param>
    /// <returns>The token.</returns>
    public static string Token(this SendRequest sent)
    {
        ArgumentNullException.ThrowIfNull(sent);

        return Token(sent.Values["link"]);
    }
}
