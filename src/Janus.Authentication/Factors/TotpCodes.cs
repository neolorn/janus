using System;
using System.Globalization;
using System.Security.Cryptography;
using OtpNet;

namespace Janus.Authentication.Factors;

/// <summary>
/// Time-based one-time codes: thirty-second steps, six digits, a drift tolerance the
/// deployment sets, and no code accepted twice.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-005. Without replay prevention an observer has up to thirty
/// seconds to reuse a code they watched being typed, so a code is refused once its
/// step has been consumed.
/// </remarks>
internal static class TotpCodes
{
    /// <summary>
    /// How long one step lasts.
    /// </summary>
    public const int StepSeconds = 30;

    /// <summary>
    /// How many digits a code carries.
    /// </summary>
    public const int Digits = 6;

    /// <summary>
    /// How many bytes a shared secret carries.
    /// </summary>
    public const int SecretLength = 20;

    /// <summary>
    /// Draws a shared secret for a new enrolment.
    /// </summary>
    /// <param name="randomness">Where the bytes are drawn from.</param>
    /// <returns>The secret.</returns>
    /// <exception cref="ArgumentNullException">The source is absent.</exception>
    public static byte[] Draw(RandomNumberGenerator randomness)
    {
        ArgumentNullException.ThrowIfNull(randomness);

        byte[] drawn = new byte[SecretLength];
        randomness.GetBytes(drawn);

        return drawn;
    }

    /// <summary>
    /// A shared secret as the person types it into an authenticator app.
    /// </summary>
    /// <param name="secret">The secret.</param>
    /// <returns>The secret in Base32, which is what the app expects.</returns>
    /// <exception cref="ArgumentNullException">The secret is absent.</exception>
    public static string Text(byte[] secret)
    {
        ArgumentNullException.ThrowIfNull(secret);

        return Base32Encoding.ToString(secret);
    }

    /// <summary>
    /// The address an authenticator app is pointed at, which the frontend shows as a
    /// QR code (FE-PM-006).
    /// </summary>
    /// <param name="issuer">What the deployment calls itself.</param>
    /// <param name="account">Which account of it the secret belongs to.</param>
    /// <param name="secret">The secret in Base32.</param>
    /// <returns>The <c>otpauth</c> address, carrying the parameters of AUTH-FACT-005.</returns>
    public static string Address(string issuer, string account, string secret) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(account)}?secret={secret}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits={Digits}&period={StepSeconds}");

    /// <summary>
    /// The step a code is valid for, where it is valid and has not been used.
    /// </summary>
    /// <param name="material">The secret and the last step a code was accepted for.</param>
    /// <param name="code">What was typed.</param>
    /// <param name="now">The instant it was typed.</param>
    /// <param name="drift">How many steps either side are accepted.</param>
    /// <returns>
    /// The step the code belongs to, or nothing where it is not a code of this
    /// secret, where it is outside the tolerance, or where its step has been
    /// consumed.
    /// </returns>
    /// <exception cref="ArgumentNullException">The material is absent.</exception>
    public static long? Accepts(
        TotpMaterial material,
        string code,
        DateTimeOffset now,
        int drift)
    {
        ArgumentNullException.ThrowIfNull(material);

        if (string.IsNullOrEmpty(code))
        {
            return null;
        }

        byte[] secret = material.Secret.ToArray();

        try
        {
            var codes = new Totp(secret, StepSeconds, OtpHashMode.Sha1, Digits);
            bool valid = codes.VerifyTotp(
                now.UtcDateTime,
                code,
                out long step,
                new VerificationWindow(previous: drift, future: drift));

            // A code from a step already spent is refused whether or not it still
            // reads as valid, which is what closes the window an observer has.
            return valid && step > (material.ConsumedStep ?? long.MinValue) ? step : null;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }
}
