using System;
using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Janus.Core;

namespace Janus.Authentication.Factors;

/// <summary>
/// The code a message carries to prove control of an address or a number: six digits,
/// compared in fixed time, living a lifetime of its own and dying after the tries the
/// deployment admits.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-004. It is not an authentication credential and no sign-in
/// endpoint takes one: it is stored apart from the factor catalogue, expires on
/// <c>code.verification.lifetime</c> rather than on whatever issued it, dies after
/// <c>code.verification.attempts</c> wrong tries, and is spent by the first right one.
/// The digits are held rather than fingerprinted because a link opened away from the
/// registering browser has to show them (REG-SESS-003); the row they are held in
/// carries what they were issued against and nothing that names a person.
/// </remarks>
[NeverLogged]
internal sealed class VerificationCode
{
    /// <summary>
    /// How many digits a code carries.
    /// </summary>
    public const int Digits = 6;

    private const int Ceiling = 1000000;

    // The largest multiple of the ceiling a drawn number can reach; a number at or above
    // it is drawn again, so every code is equally likely.
    private const uint Unbiased = uint.MaxValue / Ceiling * Ceiling;

    private VerificationCode(
        byte[] holder,
        [NeverLogged] byte[] code,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        int attempts)
    {
        Holder = holder;
        Code = code;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
        Attempts = attempts;
    }

    /// <summary>What the code was issued against, which is what finds it again.</summary>
    public byte[] Holder { get; }

    /// <summary>The digits, as they are compared.</summary>
    [NeverLogged]
    public byte[] Code { get; }

    /// <summary>When it was issued.</summary>
    public DateTimeOffset IssuedAt { get; }

    /// <summary>When it stops being answerable, whatever issued it.</summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>How many wrong codes have been entered against it.</summary>
    public int Attempts { get; private set; }

    /// <summary>
    /// Issues one.
    /// </summary>
    /// <param name="holder">What the code is issued against.</param>
    /// <param name="code">The code as the person reads it.</param>
    /// <param name="at">Now.</param>
    /// <param name="lifetime">How long it answers for.</param>
    /// <returns>The code.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static VerificationCode Issue(
        byte[] holder,
        [NeverLogged] string code,
        DateTimeOffset at,
        TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(holder);

        return new VerificationCode(holder, Held(code), at, at + lifetime, attempts: 0);
    }

    /// <summary>
    /// The record of a value that is held or reserved: one that lives and counts as an
    /// issued code does and holds no code any presentation matches (AUTH-FACT-004,
    /// REG-SESS-005).
    /// </summary>
    /// <param name="holder">What it stands against.</param>
    /// <param name="at">Now.</param>
    /// <param name="lifetime">How long it answers for.</param>
    /// <returns>The record.</returns>
    /// <exception cref="ArgumentNullException">The holder is absent.</exception>
    /// <remarks>
    /// What is presented is compared as its digits and nothing else, so a value of the
    /// length of a code that holds no digit is compared in the same fixed time and is
    /// never equal to it.
    /// </remarks>
    public static VerificationCode Unanswerable(byte[] holder, DateTimeOffset at, TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(holder);

        return new VerificationCode(holder, new byte[Digits], at, at + lifetime, attempts: 0);
    }

    /// <summary>
    /// The code as the store holds it.
    /// </summary>
    /// <param name="holder">What it was issued against.</param>
    /// <param name="code">The digits.</param>
    /// <param name="issuedAt">When it was issued.</param>
    /// <param name="expiresAt">When it stops being answerable.</param>
    /// <param name="attempts">How many wrong codes have been entered against it.</param>
    /// <returns>The code.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static VerificationCode Stored(
        byte[] holder,
        [NeverLogged] byte[] code,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        int attempts)
    {
        ArgumentNullException.ThrowIfNull(holder);
        ArgumentNullException.ThrowIfNull(code);

        return new VerificationCode(holder, code, issuedAt, expiresAt, attempts);
    }

    /// <summary>
    /// Draws a code.
    /// </summary>
    /// <param name="randomness">Where the digits are drawn from.</param>
    /// <returns>The code as the person reads it.</returns>
    /// <exception cref="ArgumentNullException">The source is absent.</exception>
    public static string Draw(RandomNumberGenerator randomness)
    {
        ArgumentNullException.ThrowIfNull(randomness);

        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        uint drawn;

        do
        {
            randomness.GetBytes(bytes);
            drawn = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        }
        while (drawn >= Unbiased);

        CryptographicOperations.ZeroMemory(bytes);

        return (drawn % Ceiling)
            .ToString(CultureInfo.InvariantCulture)
            .PadLeft(Digits, '0');
    }

    /// <summary>
    /// What is held against what the code was issued for.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <returns>The digits, as they are compared.</returns>
    /// <exception cref="ArgumentNullException">The code is absent.</exception>
    public static byte[] Held([NeverLogged] string code)
    {
        ArgumentNullException.ThrowIfNull(code);

        return Encoding.UTF8.GetBytes(Canonical(code));
    }

    /// <summary>
    /// The code as the landing page away from the registering browser shows it.
    /// </summary>
    /// <param name="held">What is held.</param>
    /// <returns>The code.</returns>
    /// <exception cref="ArgumentNullException">The value is absent.</exception>
    public static string Read(byte[] held)
    {
        ArgumentNullException.ThrowIfNull(held);

        return Encoding.UTF8.GetString(held);
    }

    /// <summary>
    /// Whether a code as typed is the one held.
    /// </summary>
    /// <param name="held">What is held.</param>
    /// <param name="entered">The code as it was typed.</param>
    /// <returns>Whether they match.</returns>
    /// <exception cref="ArgumentNullException">Either is absent.</exception>
    public static bool Matches([NeverLogged] byte[] held, [NeverLogged] string entered)
    {
        ArgumentNullException.ThrowIfNull(held);
        ArgumentNullException.ThrowIfNull(entered);

        byte[] presented = Held(entered);

        try
        {
            return CryptographicOperations.FixedTimeEquals(held, presented);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(presented);
        }
    }

    /// <summary>
    /// Whether it still answers at this instant.
    /// </summary>
    /// <param name="now">Now.</param>
    /// <returns>Whether it is live.</returns>
    public bool IsLive(DateTimeOffset now) => now < ExpiresAt;

    /// <summary>
    /// Whether the code as typed is this one.
    /// </summary>
    /// <param name="entered">The code as it was typed.</param>
    /// <returns>Whether they match.</returns>
    /// <exception cref="ArgumentNullException">The code is absent.</exception>
    public bool Is([NeverLogged] string entered) => Matches(Code, entered);

    /// <summary>
    /// Whether it holds digits somebody was sent, as against the record of a held or
    /// reserved value, which holds none.
    /// </summary>
    /// <returns>Whether it does.</returns>
    public bool IsAnswerable()
    {
        foreach (byte held in Code)
        {
            if (!char.IsAsciiDigit((char)held))
            {
                return false;
            }
        }

        return Code.Length > 0;
    }

    /// <summary>
    /// A wrong code was entered against it.
    /// </summary>
    /// <returns>How many wrong codes have now been entered.</returns>
    public int Missed() => ++Attempts;

    /// <summary>
    /// Whether the tries the deployment admits have run out.
    /// </summary>
    /// <param name="cap">How many wrong tries end the code.</param>
    /// <returns>Whether it is finished.</returns>
    public bool Exhausted(int cap) => Attempts >= cap;

    // Spaces and separators are what a person copying six digits off a screen adds.
    private static string Canonical([NeverLogged] string entered)
    {
        var canonical = new StringBuilder(Digits);

        foreach (char typed in entered)
        {
            if (char.IsAsciiDigit(typed))
            {
                _ = canonical.Append(typed);
            }
        }

        return canonical.ToString();
    }
}
