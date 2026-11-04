using Janus.Core;

namespace Janus.Authentication.Search;

/// <summary>
/// One subject a search found, with no value of it: the names shown are read per query,
/// and the contacts through the contact read.
/// </summary>
/// <param name="Subject">The subject.</param>
/// <param name="Kind">Whether it is an account or a person record.</param>
/// <param name="Matched">Which field matched first, in the order name, email, phone.</param>
/// <remarks>Implements IDN-SRCH-001 (D-256).</remarks>
internal readonly record struct IndexHit(SubjectId Subject, SubjectKind Kind, SearchMatch Matched);
