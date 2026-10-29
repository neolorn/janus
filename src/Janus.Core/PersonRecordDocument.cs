using System;

namespace Janus.Core;

/// <summary>
/// A presentation of the privacy notice, or an acceptance of the terms, a person
/// record's collection recorded.
/// </summary>
/// <param name="Version">The version presented or accepted.</param>
/// <param name="At">When.</param>
/// <remarks>Implements REC-ADM-001 and PRIV-CONS-008a.</remarks>
public sealed record PersonRecordDocument(string Version, DateTimeOffset At);
