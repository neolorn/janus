using Janus.Core;

namespace Janus.Storage;

/// <summary>
/// Where a personal value is stored. A ciphertext is bound to its location, so a value
/// moved to another row, another column or another subject no longer decrypts.
/// </summary>
/// <param name="Subject">The subject whose key the value is encrypted under.</param>
/// <param name="Table">The table holding it.</param>
/// <param name="Column">The column holding it.</param>
/// <remarks>Implements PRIV-RIGHT-005a.</remarks>
internal readonly record struct PersonalFieldLocation(SubjectId Subject, string Table, string Column);
