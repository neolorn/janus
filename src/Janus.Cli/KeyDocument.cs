using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Cli;

/// <summary>
/// The database connection and the key material a command runs with, read once from
/// standard input.
/// </summary>
/// <remarks>
/// Implements OPS-SEC-001, INF-HOST-003 and LIB-EXT-001, as entries 307 and 340 of the
/// decisions pending review settle them. The operator pipes the document from the
/// secrets manager's own client, so no key is an argument the process list shows, a
/// file left on disk or a variable in the environment. Standard input that is a terminal is
/// refused before anything is read, so no key is typed or pasted where a screen or a
/// history keeps it. The document is read as bytes and every key is decoded straight
/// from them into arrays the command's <see cref="HeldKeys"/> clears when it ends, so
/// nothing but the connection passes through a string. The secret of a client being
/// registered travels the same way, for the command that registers it.
/// </remarks>
[NeverLogged]
internal sealed class KeyDocument
{
    /// <summary>
    /// The member the secret of the client a command registers is read from.
    /// </summary>
    public const string ClientSecretMember = "clientSecret";

    // A connection and a handful of keys; anything longer is not the document.
    private const int Longest = 64 * 1024;

    private KeyDocument(
        string connection,
        KeyEncryptionKeys keyEncryptionKeys,
        FingerprintKeys fingerprintKeys,
        ReadOnlyMemory<byte>? clientSecret)
    {
        Connection = connection;
        KeyEncryptionKeys = keyEncryptionKeys;
        FingerprintKeys = fingerprintKeys;
        ClientSecret = clientSecret;
    }

    /// <summary>
    /// The connection the command runs under, which carries the database credential.
    /// </summary>
    public string Connection { get; }

    /// <summary>
    /// The key-encryption key and the versions retained beside it.
    /// </summary>
    public KeyEncryptionKeys KeyEncryptionKeys { get; }

    /// <summary>
    /// The key the searchable fingerprints are computed under and the versions retained
    /// beside it.
    /// </summary>
    public FingerprintKeys FingerprintKeys { get; }

    /// <summary>
    /// The secret of the client a command registers, as its UTF-8 bytes, where the
    /// document carries one.
    /// </summary>
    public ReadOnlyMemory<byte>? ClientSecret { get; }

    /// <summary>
    /// Reads the document from standard input.
    /// </summary>
    /// <param name="terminal">Where it is read from.</param>
    /// <param name="held">What clears every key read when the command ends.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>
    /// The document, or the failure naming what was missing: the keys where standard
    /// input is a terminal or holds none that can be used, the member where the
    /// document is not one.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static async ValueTask<Result<KeyDocument>> ReadAsync(
        Terminal terminal,
        HeldKeys held,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(terminal);
        ArgumentNullException.ThrowIfNull(held);

        if (!terminal.IsInputRedirected)
        {
            return Result.Failure<KeyDocument>(Unavailable("input"));
        }

        byte[] read = new byte[4096];
        int length = 0;

        try
        {
            while (true)
            {
                if (length == read.Length)
                {
                    if (read.Length >= Longest)
                    {
                        return Result.Failure<KeyDocument>(Malformed("input"));
                    }

                    byte[] grown = new byte[read.Length * 2];
                    read.AsSpan().CopyTo(grown);
                    CryptographicOperations.ZeroMemory(read);
                    read = grown;
                }

                int taken = await terminal.Input
                    .ReadAsync(read.AsMemory(length), cancellationToken)
                    .ConfigureAwait(false);

                if (taken is 0)
                {
                    break;
                }

                length += taken;
            }

            return Parsed(read.AsMemory(0, length), held);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(read);
        }
    }

    private static Result<KeyDocument> Parsed(ReadOnlyMemory<byte> text, HeldKeys held)
    {
        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            return Result.Failure<KeyDocument>(Malformed("input"));
        }

        using (document)
        {
            JsonElement root = document.RootElement;

            if (root.ValueKind is not JsonValueKind.Object)
            {
                return Result.Failure<KeyDocument>(Malformed("input"));
            }

            if (!root.TryGetProperty("connection", out JsonElement connection)
                || connection.ValueKind is not JsonValueKind.String
                || string.IsNullOrWhiteSpace(connection.GetString()))
            {
                return Result.Failure<KeyDocument>(Malformed("connection"));
            }

            if (Versions(root, "keyEncryptionKeys", held, length => length is 16 or 24 or 32)
                is not (int keyVersion, Dictionary<int, ReadOnlyMemory<byte>> keyMaterial))
            {
                return Result.Failure<KeyDocument>(Unavailable("keyEncryptionKeys"));
            }

            if (Versions(root, "fingerprintKeys", held, length => length >= FingerprintKeys.MinimumLength)
                is not (int fingerprintVersion, Dictionary<int, ReadOnlyMemory<byte>> fingerprintMaterial))
            {
                return Result.Failure<KeyDocument>(Unavailable("fingerprintKeys"));
            }

            ReadOnlyMemory<byte>? clientSecret = null;

            if (root.TryGetProperty(ClientSecretMember, out JsonElement secret))
            {
                if (secret.ValueKind is not JsonValueKind.String
                    || !secret.TryGetBytesFromBase64(out byte[]? decoded))
                {
                    return Result.Failure<KeyDocument>(Malformed(ClientSecretMember));
                }

                clientSecret = held.Hold(decoded);
            }

            return Result.Success(new KeyDocument(
                connection.GetString()!,
                new KeyEncryptionKeys(keyVersion, keyMaterial),
                new FingerprintKeys(fingerprintVersion, fingerprintMaterial),
                clientSecret));
        }
    }

    // A key's versions, each held where it can be cleared, or nothing where the member
    // does not name a current version among keys of a length the key takes.
    private static (int Current, Dictionary<int, ReadOnlyMemory<byte>> Material)? Versions(
        JsonElement root,
        string name,
        HeldKeys held,
        Func<int, bool> usable)
    {
        if (!root.TryGetProperty(name, out JsonElement member)
            || member.ValueKind is not JsonValueKind.Object
            || !member.TryGetProperty("current", out JsonElement current)
            || !current.TryGetInt32(out int currentVersion)
            || !member.TryGetProperty("versions", out JsonElement versions)
            || versions.ValueKind is not JsonValueKind.Object)
        {
            return null;
        }

        var material = new Dictionary<int, ReadOnlyMemory<byte>>();

        foreach (JsonProperty version in versions.EnumerateObject())
        {
            if (!int.TryParse(version.Name, NumberStyles.None, CultureInfo.InvariantCulture, out int number)
                || version.Value.ValueKind is not JsonValueKind.String
                || !version.Value.TryGetBytesFromBase64(out byte[]? key))
            {
                return null;
            }

            if (!usable(held.Hold(key).Length) || !material.TryAdd(number, key))
            {
                return null;
            }
        }

        return material.ContainsKey(currentVersion) ? (currentVersion, material) : null;
    }

    private static Error Unavailable(string member) =>
        Error.From(ErrorCodes.StartupKeyUnavailable, "member", JsonSerializer.SerializeToElement(member));

    private static Error Malformed(string member) =>
        Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement(member));
}
