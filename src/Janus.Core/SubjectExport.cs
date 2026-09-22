using System;
using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// Everything the library holds about one subject, assembled once. The two formats
/// of PRIV-RIGHT-003 are two arrangements of this and not two assemblies of it.
/// </summary>
/// <param name="Subject">Whose it is.</param>
/// <param name="AssembledAt">When it was assembled.</param>
/// <param name="Sections">
/// The groups, in reading order: the account and how it was registered, the profile,
/// the identifiers, the preferences, the live sessions and their location records,
/// and the consents and objections.
/// </param>
/// <remarks>
/// Implements PRIV-RIGHT-003 and LIB-API-005. What the host holds in its own tables
/// is the host's to return, and reaches it as the <c>ExportRequested</c> event this
/// assembly raises (PRIV-RIGHT-005b).
/// </remarks>
public sealed record SubjectExport(
    SubjectId Subject,
    DateTimeOffset AssembledAt,
    IReadOnlyList<ExportSection> Sections);
