using System;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Janus.Core;

namespace Janus.Authentication;

/// <summary>
/// A value a browser carries that says nothing: thirty-two drawn bytes, held server
/// side by their fingerprint so that a database dump yields no usable token.
/// </summary>
/// <remarks>
/// Implements AUTH-SESS-003, AUTH-FACT-015 and AUTH-FACT-016. The same shape serves
/// the session cookie, the trusted-device token and the remembered browser.
/// </remarks>
[NeverLogged]
internal readonly record struct OpaqueToken
{
    private const int Length = 32;

    private OpaqueToken(string value) => Value = value;

    /// <summary>
    /// How wide a drawn token is written, which is what a message carrying one is
    /// measured against its budget with (INT-SMS-003).
    /// </summary>
    public static int Width { get; } = Base64Url.EncodeToString(new byte[Length]).Length;

    /// <summary>
    /// The value as the cookie carries it.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Draws a token.
    /// </summary>
    /// <param name="randomness">Where the bytes are drawn from.</param>
    /// <returns>The token.</returns>
    /// <exception cref="ArgumentNullException">The source is absent.</exception>
    public static OpaqueToken Draw(RandomNumberGenerator randomness)
    {
        ArgumentNullException.ThrowIfNull(randomness);

        byte[] drawn = new byte[Length];
        randomness.GetBytes(drawn);

        return new OpaqueToken(Base64Url.EncodeToString(drawn));
    }

    /// <summary>
    /// The token a request presented.
    /// </summary>
    /// <param name="value">The value the request carried.</param>
    /// <returns>The token.</returns>
    /// <exception cref="ArgumentException">The value is unset.</exception>
    public static OpaqueToken Of(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        return new OpaqueToken(value);
    }

    /// <summary>
    /// What is stored against the record, which the token cannot be recovered from.
    /// </summary>
    /// <returns>The fingerprint.</returns>
    public byte[] Fingerprint() => SHA256.HashData(Encoding.UTF8.GetBytes(Value));

    /// <inheritdoc/>
    public override string ToString() => Value;
}
