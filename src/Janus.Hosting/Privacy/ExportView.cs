using System;
using System.Collections.Generic;

namespace Janus.Hosting.Privacy;

/// <summary>
/// The readable arrangement of an export: grouped, labelled and ordered, and still
/// structured data rather than a rendered document.
/// </summary>
/// <param name="Subject">Whose export it is.</param>
/// <param name="AssembledAt">When it was assembled.</param>
/// <param name="Sections">The groups, in reading order.</param>
/// <remarks>Implements PRIV-RIGHT-003 and D-054.</remarks>
internal sealed record ExportView(
    string Subject,
    DateTimeOffset AssembledAt,
    IReadOnlyList<ExportSectionView> Sections);
