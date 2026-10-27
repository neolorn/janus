using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// Where a person stands for a host's own processing: the state of the account, which
/// says whether its processing is restricted, and the consents and objections it holds.
/// </summary>
/// <param name="State">Where the account stands.</param>
/// <param name="Consents">
/// Every consent record held for the person, withdrawn and superseded ones included, as
/// the person's own read answers them.
/// </param>
/// <param name="Objections">
/// Every objection record held for the person, withdrawn ones included, as the person's
/// own read answers them.
/// </param>
/// <remarks>Implements IDN-READ-003 and PRIV-RIGHT-004.</remarks>
public sealed record SubjectStanding(
    AccountState State,
    IReadOnlyList<ConsentRecord> Consents,
    IReadOnlyList<ObjectionRecord> Objections);
