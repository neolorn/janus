namespace Janus.Core;

/// <summary>
/// One encrypted field of a host's record, and the column naming the subject whose
/// key encrypts it. Without the second, erasure could not reach the field.
/// </summary>
/// <param name="Field">The field held as ciphertext.</param>
/// <param name="SubjectColumn">The column naming the subject whose key encrypts it.</param>
/// <remarks>Implements AUTHZ-MODEL-003, PRIV-RIGHT-005a.</remarks>
public sealed record EncryptedFieldDeclaration(string Field, string SubjectColumn);
