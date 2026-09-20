using System;
using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// A generated set of recovery codes: the codes themselves, which leave the library
/// once, and when the set was made.
/// </summary>
/// <param name="Codes">The codes, returned once and never read back.</param>
/// <param name="GeneratedAt">
/// When the set was made, which is what the reminder of AUTH-FACT-008 is measured
/// from.
/// </param>
/// <remarks>Implements AUTH-FACT-008, AUTH-FACT-009 and AUTH-RECOV-006.</remarks>
public sealed record GeneratedRecoveryCodes(
    IReadOnlyList<string> Codes,
    DateTimeOffset GeneratedAt);
