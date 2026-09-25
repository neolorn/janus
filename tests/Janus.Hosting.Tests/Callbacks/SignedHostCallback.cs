using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting.Callbacks;
using Microsoft.Extensions.Primitives;

namespace Janus.Hosting.Tests.Callbacks;

/// <summary>
/// A host's callback from a provider that signs: an HMAC-SHA256 over the instant and
/// the raw body, in hexadecimal, as a common provider scheme does; the event
/// identifier is the body's <c>id</c>.
/// </summary>
internal sealed class SignedHostCallback : ISignedCallback
{
    /// <summary>The header the signature arrives in.</summary>
    public const string SignatureHeader = "X-Provider-Signature";

    /// <summary>The header the instant arrives in, as Unix seconds.</summary>
    public const string InstantHeader = "X-Provider-Timestamp";

    /// <inheritdoc/>
    public string Name => "provider-events";

    /// <inheritdoc/>
    public IReadOnlyCollection<IPNetwork> Sources { get; set; } = [];

    /// <inheritdoc/>
    public HashAlgorithmName Algorithm { get; set; } = HashAlgorithmName.SHA256;

    /// <summary>
    /// The secrets the secrets manager holds; nothing where it cannot give them.
    /// </summary>
    public CallbackSecrets? Secrets { get; set; } =
        new(Encoding.UTF8.GetBytes("the-current-secret"), DateTimeOffset.UnixEpoch, Previous: null);

    /// <summary>
    /// How many bodies were parsed, which is never before a signature held.
    /// </summary>
    public int Parsed { get; private set; }

    /// <summary>
    /// Signs a body the way the provider does.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <param name="at">When it is signed.</param>
    /// <param name="secret">Under what.</param>
    /// <returns>The headers the provider sends with it.</returns>
    public static (string Name, string Value)[] Signing(string body, DateTimeOffset at, string secret)
    {
        string instant = at.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        byte[] signature = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes(instant + "." + body));

        return [(SignatureHeader, Convert.ToHexStringLower(signature)), (InstantHeader, instant)];
    }

    /// <inheritdoc/>
    public ValueTask<Result<CallbackSecrets>> ReadSecretsAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Secrets is CallbackSecrets secrets
            ? Result.Success(secrets)
            : Result.Failure<CallbackSecrets>(Error.From(ErrorCodes.CallbackRejected)));

    /// <inheritdoc/>
    public Result<CallbackSignature> Presented(CallbackDelivery delivery)
    {
        if (!delivery.Headers.TryGetValue(SignatureHeader, out StringValues signature)
            || !delivery.Headers.TryGetValue(InstantHeader, out StringValues instant)
            || !long.TryParse(instant.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out long seconds)
            || signature.ToString() is not { Length: > 0 } hex
            || hex.Length % 2 != 0
            || !hex.All(Uri.IsHexDigit))
        {
            return Result.Failure<CallbackSignature>(Error.From(ErrorCodes.CallbackRejected));
        }

        byte[] prefix = Encoding.UTF8.GetBytes(instant.ToString() + ".");
        byte[] message = [.. prefix, .. delivery.Body.Span];

        return Result.Success(new CallbackSignature(
            [Convert.FromHexString(hex)],
            message,
            DateTimeOffset.FromUnixTimeSeconds(seconds)));
    }

    /// <inheritdoc/>
    public Result<string> EventOf(CallbackDelivery delivery)
    {
        Parsed++;

        using var document = JsonDocument.Parse(delivery.Body);

        return document.RootElement.TryGetProperty("id", out JsonElement identifier)
            && identifier.GetString() is { Length: > 0 } value
            ? Result.Success(value)
            : Result.Failure<string>(Error.From(ErrorCodes.CallbackRejected));
    }
}
