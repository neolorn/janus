using System;
using Janus.Core;

namespace Janus.Privacy.SubjectKeys;

/// <summary>
/// A row of the subject-key table: a subject's key, or the deployment's data key under
/// the identifier no subject is issued.
/// </summary>
/// <param name="Value">The row's key as the database carries it.</param>
/// <remarks>
/// Implements PRIV-RIGHT-005a, OPS-SEC-003 and CONV-DESIGN-004. It is not a subject
/// identifier (D-174): the key rotation walks the table in this key's order, and the
/// last row it reaches is the deployment's, which no <see cref="SubjectId"/> can name.
/// </remarks>
internal readonly record struct SubjectKeyId(Guid Value)
{
    /// <summary>
    /// The deployment's data key's row: the max UUID of RFC 9562, all 128 bits set,
    /// which the version 4 subject identifiers never take. The nil subject is not used,
    /// since it already means no subject.
    /// </summary>
    public static SubjectKeyId Deployment { get; } = new(Guid.AllBitsSet);

    /// <summary>
    /// The row that holds a subject's key.
    /// </summary>
    /// <param name="subject">Whose key.</param>
    /// <returns>The row.</returns>
    public static SubjectKeyId Of(SubjectId subject) => new(subject.Value);
}
