using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// One thing a derivation confers, as the host's rows and the ancestry say it: a
/// holder, a record of the type the derivation is declared on, and the record the
/// holder's row names, which is that record or one containing it.
/// </summary>
/// <param name="Holder">The subject the row names.</param>
/// <param name="Record">The record the derivation confers the role on.</param>
/// <param name="Named">The record the relationship's row names.</param>
/// <remarks>
/// Implements AUTHZ-DERIVE-005. This is what a materialised derivation's grants are held
/// against by the drift check.
/// </remarks>
internal sealed record ConferredRecord(SubjectId Holder, ResourceId Record, ResourceId Named);
