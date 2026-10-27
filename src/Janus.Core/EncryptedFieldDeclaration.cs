namespace Janus.Core;

/// <summary>
/// One encrypted field of a host's record, the column naming the subject whose key
/// encrypts it, and the data category it holds. Without the second, erasure could not
/// reach the field; without the third, no purpose would account for holding it.
/// </summary>
/// <param name="Field">The field held as ciphertext.</param>
/// <param name="SubjectColumn">The column naming the subject whose key encrypts it.</param>
/// <param name="Category">The data category it holds, which a purpose on its type names.</param>
/// <param name="Name">
/// The name its values are bound to: the member's own name, or the name the declaration
/// gives it, so that a member the host renames keeps its values readable.
/// </param>
/// <remarks>Implements AUTHZ-MODEL-003, PRIV-RIGHT-005a, PRIV-PRIN-001.</remarks>
public sealed record EncryptedFieldDeclaration(string Field, string SubjectColumn, string Category, string Name);
