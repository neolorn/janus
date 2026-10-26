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
/// Implements OPS-SEC-001, INF-HOST-003 and LIB-EXT-001, as entry 307 of the decisions
/// pending review and D-166 (340) settle them. The operator pipes the document from the
/// secrets manager's own client, so no key is an argument the process list shows, a
/// file left on disk or a variable in the environment. Standard input that is a terminal is
/// refused before anything is read, so no key is typed or pasted where a screen or a
/// history keeps it. The document is read as bytes and every key is decoded straight
/// from them into arrays cleared once the command's key ring holds its own copies, so
/// nothing but the connection passes through a string; the ring is filled once, at the
/// command's start, and cleared when the command ends (CONV-CODE-007).
/// </remarks>
[NeverLogged]
internal sealed class KeyDocument
{
    // A connection and a handful of keys; anything longer is not the document.
    private const int Longest = 64 * 1024;

    /// <summary>
    /// The members of the document every command but <c>migrate</c> reads, each with the
    /// type it is written as (LIB-API-001); a version's key is base64.
    /// </summary>
    public static IReadOnlyList<string> Members { get; } =
    [
        "connection: string",
        "keyEncryptionKeys.current: number",
        "keyEncryptionKeys.versions{}: string",
        "fingerprintKeys.current: number",
        "fingerprintKeys.versions{}: string",
    ];

    /// <summary>
    /// The one member of the document <c>migrate</c> reads (OPS-MIG-001).
    /// </summary>
    public static IReadOnlyList<string> ConnectionMembers { get; } = ["connection: string"];

    private KeyDocument(string connection, IKeyRing ring)
    {
        Connection = connection;
        Ring = ring;
    }

    /// <summary>
    /// The connection the command runs under, which carries the database credential.
    /// </summary>
    public string Connection { get; }

    /// <summary>
    /// The key ring the document's keys were read into: the key-encryption key and the
    /// fingerprint key, each with the versions retained beside it.
    /// </summary>
    public IKeyRing Ring { get; }

    /// <summary>
    /// Reads the document from standard input.
    /// </summary>
    /// <param name="terminal">Where it is read from.</param>
    /// <param name="held">The key ring the keys are read into, and what clears every key read.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>
    /// The document, or the failure naming what was missing: <c>input</c> where standard
    /// input is a terminal or the document itself does not read, the key where it holds
    /// none that can be used, the member where one it carries is not of its form.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static async ValueTask<Result<KeyDocument>> ReadAsync(
        Terminal terminal,
        HeldKeys held,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(terminal);
        ArgumentNullException.ThrowIfNull(held);

        try
        {
            return await ReadAsync(terminal, text => Parsed(text, held), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            held.Release();
        }
    }

    /// <summary>
    /// Reads the document of <c>migrate</c> from standard input, which carries the
    /// connection and nothing else.
    /// </summary>
    /// <param name="terminal">Where it is read from.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>
    /// The connection, or the failure: malformed naming <c>input</c> where the document
    /// carries any member but the connection or does not read, malformed naming
    /// <c>connection</c> where that member is blank or not a string, and unavailable
    /// naming <c>input</c> where standard input is a terminal or the document carries no
    /// connection member.
    /// </returns>
    /// <exception cref="ArgumentNullException">The terminal is absent.</exception>
    /// <remarks>
    /// Implements OPS-MIG-001 AC4 and D-173: the migration step never holds a key, so a
    /// document carrying one is refused before its connection is read.
    /// </remarks>
    public static ValueTask<Result<string>> ConnectionAsync(Terminal terminal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(terminal);

        return ReadAsync(terminal, ConnectionAlone, cancellationToken);
    }

    // The whole of standard input, read as bytes into an array cleared once it is
    // parsed, so that nothing but what the parse keeps passes through a string.
    private static async ValueTask<Result<TValue>> ReadAsync<TValue>(
        Terminal terminal,
        Func<ReadOnlyMemory<byte>, Result<TValue>> parsed,
        CancellationToken cancellationToken)
    {
        if (!terminal.IsInputRedirected)
        {
            return Result.Failure<TValue>(Unavailable("input"));
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
                        return Result.Failure<TValue>(Malformed("input"));
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

            return parsed(read.AsMemory(0, length));
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
                return Result.Failure<KeyDocument>(Unavailable("input"));
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

            held.Ring.HoldKeyEncryptionKeys(new KeyEncryptionKeys(keyVersion, keyMaterial));
            held.Ring.HoldFingerprintKeys(new FingerprintKeys(fingerprintVersion, fingerprintMaterial));
            held.Ring.Fill();

            return Result.Success(new KeyDocument(connection.GetString()!, held.Ring));
        }
    }

    // OPS-MIG-001: a member beside the connection is refused first, whether or not the
    // connection is there; a connection blank or not a string is refused as the other
    // commands refuse it (D-209).
    private static Result<string> ConnectionAlone(ReadOnlyMemory<byte> text)
    {
        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            return Result.Failure<string>(Malformed("input"));
        }

        using (document)
        {
            JsonElement root = document.RootElement;

            if (root.ValueKind is not JsonValueKind.Object)
            {
                return Result.Failure<string>(Unavailable("input"));
            }

            foreach (JsonProperty member in root.EnumerateObject())
            {
                if (!member.NameEquals("connection"))
                {
                    return Result.Failure<string>(Malformed("input"));
                }
            }

            if (!root.TryGetProperty("connection", out JsonElement connection))
            {
                return Result.Failure<string>(Unavailable("input"));
            }

            return connection.ValueKind is JsonValueKind.String
                && !string.IsNullOrWhiteSpace(connection.GetString())
                    ? Result.Success(connection.GetString()!)
                    : Result.Failure<string>(Malformed("connection"));
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

    private static Error Unavailable(string key) =>
        Error.From(ErrorCodes.StartupSecretUnavailable, "key", JsonSerializer.SerializeToElement(key));

    private static Error Malformed(string member) =>
        Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement(member));
}
