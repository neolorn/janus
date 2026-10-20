using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Janus.Core;

namespace Janus.Storage.Privacy.SubjectKeys;

/// <summary>
/// One subject's data key, lent to a host's use for as long as the use runs.
/// </summary>
/// <param name="subject">The subject the key is held under.</param>
/// <param name="dataKey">The unwrapped data key, or nothing where the subject's key is erased.</param>
/// <param name="declared">The host's fields each of its types declares encrypted, by their declared names.</param>
/// <param name="randomness">The randomness an initialisation vector is drawn from.</param>
/// <remarks>
/// Implements PRIV-RIGHT-005a and CONV-CODE-007 (D-203). The key is cleared when the use
/// returns, and from then on every member is a fault, so nothing the use kept can reach
/// it.
/// </remarks>
internal sealed class LentFields(
    SubjectId subject,
    byte[]? dataKey,
    IReadOnlySet<(ResourceType Type, string Field)> declared,
    RandomNumberGenerator randomness) : SubjectFields
{
    private bool _returned;

    /// <inheritdoc/>
    public override bool Erased
    {
        get
        {
            ObjectDisposedException.ThrowIf(_returned, this);

            return dataKey is null;
        }
    }

    /// <inheritdoc/>
    public override Result<ReadOnlyMemory<byte>> Encrypt(ResourceType type, string field, string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return Encrypt(type, field, Encoding.UTF8.GetBytes(value));
    }

    /// <inheritdoc/>
    public override Result<ReadOnlyMemory<byte>> Encrypt(ResourceType type, string field, ReadOnlyMemory<byte> value)
    {
        HostFieldLocation location = Located(type, field);

        if (dataKey is null)
        {
            return Result.Failure<ReadOnlyMemory<byte>>(Error.From(ErrorCodes.SubjectErased));
        }

        return Result.Success<ReadOnlyMemory<byte>>(
            PersonalFieldCipher.Encrypt(dataKey, location, value.Span, randomness));
    }

    /// <inheritdoc/>
    public override Result<string> Decrypt(ResourceType type, string field, ReadOnlyMemory<byte> stored)
    {
        HostFieldLocation location = Located(type, field);

        if (dataKey is null)
        {
            return Result.Failure<string>(Error.From(ErrorCodes.SubjectErased));
        }

        byte[] plaintext = PersonalFieldCipher.Decrypt(dataKey, location, stored.Span);

        return Result.Success(Encoding.UTF8.GetString(plaintext));
    }

    /// <inheritdoc/>
    public override Result<ReadOnlyMemory<byte>> DecryptBytes(ResourceType type, string field, ReadOnlyMemory<byte> stored)
    {
        HostFieldLocation location = Located(type, field);

        if (dataKey is null)
        {
            return Result.Failure<ReadOnlyMemory<byte>>(Error.From(ErrorCodes.SubjectErased));
        }

        return Result.Success<ReadOnlyMemory<byte>>(PersonalFieldCipher.Decrypt(dataKey, location, stored.Span));
    }

    /// <summary>
    /// Ends the loan: the key is cleared, and every later use is a fault.
    /// </summary>
    public void Return()
    {
        _returned = true;

        if (dataKey is not null)
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    // A field the type does not declare encrypted is the calling code's fault, never a
    // refusal: the declaration is the host's own, and a value written under a name it
    // does not declare would be personal data no declared purpose accounts for.
    private HostFieldLocation Located(ResourceType type, string field)
    {
        ObjectDisposedException.ThrowIf(_returned, this);
        ArgumentNullException.ThrowIfNull(field);

        if (!declared.Contains((type, field)))
        {
            throw new ArgumentException(
                "The type " + type + " declares no encrypted field of that name.",
                nameof(field));
        }

        return new HostFieldLocation(subject, type, field);
    }
}
