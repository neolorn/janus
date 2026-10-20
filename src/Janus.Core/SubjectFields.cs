using System;

namespace Janus.Core;

/// <summary>
/// One subject's key, lent for the length of one use, through which the host encrypts
/// and reads the fields it declared encrypted under that subject.
/// </summary>
/// <remarks>
/// Implements PRIV-RIGHT-005a and LIB-API-001 (D-203). The library hands one of these to
/// the use a host passes to <see cref="IPersonalFields"/> and implements it itself; the
/// key it holds is cleared when the use returns, so one used after that is a fault. A
/// value is bound to the subject, the type's declared name and the field's declared
/// name, so it does not decrypt once moved to another subject, type or field, or into a
/// column of the library's. A field its type does not declare encrypted is a fault of
/// the calling code, and a stored value that does not authenticate is a broken
/// invariant; neither is a refusal.
/// </remarks>
public abstract class SubjectFields
{
    /// <summary>
    /// Makes the lent key's view. Only the library makes one.
    /// </summary>
    protected SubjectFields()
    {
    }

    /// <summary>
    /// Whether the subject's key has been erased, in which case nothing is written or
    /// read under it.
    /// </summary>
    public abstract bool Erased { get; }

    /// <summary>
    /// Encrypts the value of a text field for storing.
    /// </summary>
    /// <param name="type">The declared resource type the field belongs to.</param>
    /// <param name="field">The field's declared name.</param>
    /// <param name="value">The value.</param>
    /// <returns>
    /// The value to store, or <c>privacy.subject.erased</c> where the subject's key is
    /// erased.
    /// </returns>
    /// <exception cref="ArgumentException">The type does not declare the field encrypted.</exception>
    /// <exception cref="ObjectDisposedException">The use this was lent to has returned.</exception>
    public abstract Result<ReadOnlyMemory<byte>> Encrypt(ResourceType type, string field, string value);

    /// <summary>
    /// Encrypts the value of a binary field for storing.
    /// </summary>
    /// <param name="type">The declared resource type the field belongs to.</param>
    /// <param name="field">The field's declared name.</param>
    /// <param name="value">The value.</param>
    /// <returns>
    /// The value to store, or <c>privacy.subject.erased</c> where the subject's key is
    /// erased.
    /// </returns>
    /// <exception cref="ArgumentException">The type does not declare the field encrypted.</exception>
    /// <exception cref="ObjectDisposedException">The use this was lent to has returned.</exception>
    public abstract Result<ReadOnlyMemory<byte>> Encrypt(ResourceType type, string field, ReadOnlyMemory<byte> value);

    /// <summary>
    /// Reads a stored text field.
    /// </summary>
    /// <param name="type">The declared resource type the field belongs to.</param>
    /// <param name="field">The field's declared name.</param>
    /// <param name="stored">The value as it is stored.</param>
    /// <returns>
    /// The value, or <c>privacy.subject.erased</c> where the subject's key is erased.
    /// </returns>
    /// <exception cref="ArgumentException">The type does not declare the field encrypted.</exception>
    /// <exception cref="ObjectDisposedException">The use this was lent to has returned.</exception>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// The stored value does not authenticate: it was altered, or moved from another
    /// subject, type or field.
    /// </exception>
    public abstract Result<string> Decrypt(ResourceType type, string field, ReadOnlyMemory<byte> stored);

    /// <summary>
    /// Reads a stored binary field.
    /// </summary>
    /// <param name="type">The declared resource type the field belongs to.</param>
    /// <param name="field">The field's declared name.</param>
    /// <param name="stored">The value as it is stored.</param>
    /// <returns>
    /// The value, or <c>privacy.subject.erased</c> where the subject's key is erased.
    /// </returns>
    /// <exception cref="ArgumentException">The type does not declare the field encrypted.</exception>
    /// <exception cref="ObjectDisposedException">The use this was lent to has returned.</exception>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// The stored value does not authenticate: it was altered, or moved from another
    /// subject, type or field.
    /// </exception>
    public abstract Result<ReadOnlyMemory<byte>> DecryptBytes(ResourceType type, string field, ReadOnlyMemory<byte> stored);
}
