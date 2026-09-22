using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// One thing an export section holds: a set of named values, all of them written out
/// as text so that what a field means never depends on how it was typed.
/// </summary>
/// <param name="Values">
/// The field names and what they hold. An instant is written in the round-trip form,
/// a flag as <c>true</c> or <c>false</c>, and a field the account has nothing in is
/// absent rather than empty.
/// </param>
/// <remarks>
/// Implements PRIV-RIGHT-003 and LIB-API-003. The names are the stable part of the
/// contract and carry no wording a person reads: the frontend writes the labels
/// (CONV-CONTENT-001).
/// </remarks>
public sealed record ExportRecord(IReadOnlyDictionary<string, string> Values);
