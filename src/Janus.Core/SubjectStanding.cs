using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// Where a person stands for a host's own processing: the state of the account, which
/// says whether its processing is restricted, and the consents and objections it holds.
/// </summary>
/// <param name="Kind">Whether the subject is an account or a person record.</param>
/// <param name="State">Where the account or the record stands.</param>
/// <param name="Consents">
/// Every consent record held for the person, withdrawn and superseded ones included, as
/// the person's own read answers them.
/// </param>
/// <param name="Objections">
/// Every objection record held for the person, withdrawn ones included, as the person's
/// own read answers them.
/// </param>
/// <remarks>Implements IDN-READ-003, PRIV-RIGHT-004 and REC-ADM-001.</remarks>
public sealed record SubjectStanding(
    SubjectKind Kind,
    AccountState State,
    IReadOnlyList<ConsentRecord> Consents,
    IReadOnlyList<ObjectionRecord> Objections);
