using System;

namespace Janus.Authentication.Sending;

/// <summary>
/// One key a send counts against, with the span beyond which its times decide
/// nothing and the record is pruned.
/// </summary>
/// <param name="Key">The restriction and the value.</param>
/// <param name="Retain">The longest interval any of the restriction's buckets counts over.</param>
/// <remarks>
/// Implements AUTH-ABUSE-004: the per-destination record is deleted once its buckets
/// are empty, so the ledger is told how long emptiness takes.
/// </remarks>
internal sealed record SendCount(RestrictionKey Key, TimeSpan Retain);
