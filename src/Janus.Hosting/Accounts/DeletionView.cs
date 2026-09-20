using System;

namespace Janus.Hosting.Accounts;

/// <summary>
/// What the account is told when its deletion is accepted: when the erasure runs if
/// nothing cancels it.
/// </summary>
/// <param name="ErasesAt">The instant the grace window ends.</param>
/// <remarks>Implements IDN-LIFE-014 and chapter 09 section 6.</remarks>
internal sealed record DeletionView(DateTimeOffset ErasesAt);
