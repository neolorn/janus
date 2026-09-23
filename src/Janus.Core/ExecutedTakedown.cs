using System;

namespace Janus.Core;

/// <summary>
/// What the trigger of a takedown answers with.
/// </summary>
/// <param name="Id">What the takedown is held under.</param>
/// <param name="ErasureDue">When the grace window ends and the erasure runs.</param>
/// <remarks>Implements IDN-LIFE-003 and chapter 09 section 8a.</remarks>
public sealed record ExecutedTakedown(TakedownId Id, DateTimeOffset ErasureDue);
