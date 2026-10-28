using System;

namespace Janus.Identity.Records;

/// <summary>
/// What one collection presented to the person and what they accepted: the version of
/// the privacy notice, and of the terms where the host asked for their acceptance.
/// </summary>
/// <param name="NoticeVersion">The version of the privacy notice the screen presented.</param>
/// <param name="TermsVersion">The version of the terms accepted, where the screen asked.</param>
/// <param name="RecordedAt">When the collection recorded them.</param>
/// <remarks>Implements REC-REC-001, REC-COL-002 and PRIV-CONS-008a.</remarks>
internal sealed record RecordDocuments(string NoticeVersion, string? TermsVersion, DateTimeOffset RecordedAt);
