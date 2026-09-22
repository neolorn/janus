using System;
using System.Collections.Generic;

namespace Janus.Hosting.Privacy;

/// <summary>
/// The portable arrangement of an export: one flat object whose every name is
/// <c>section.index.field</c>, so that a name means the same thing in every export
/// this library produces.
/// </summary>
/// <param name="Subject">Whose export it is.</param>
/// <param name="AssembledAt">When it was assembled.</param>
/// <param name="Values">
/// Every value of the export, by its stable name. It carries exactly what the
/// readable arrangement carries (PRIV-RIGHT-003 AC1).
/// </param>
/// <remarks>Implements PRIV-RIGHT-003 and D-054.</remarks>
internal sealed record PortableExportView(
    string Subject,
    DateTimeOffset AssembledAt,
    IReadOnlyDictionary<string, string> Values);
