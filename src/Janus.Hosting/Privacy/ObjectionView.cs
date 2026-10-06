using System;
using Janus.Core;

namespace Janus.Hosting.Privacy;

/// <summary>
/// One objection record as chapter 09 section 7 gives it.
/// </summary>
/// <param name="Purpose">Which purpose.</param>
/// <param name="Document">The legal document it was recorded against.</param>
/// <param name="NoticeVersion">The version of that document displayed when it was recorded.</param>
/// <param name="Mechanism">Where it was recorded.</param>
/// <param name="RecordedAt">When it was recorded.</param>
/// <param name="WithdrawnAt">When it was taken back, where it was.</param>
/// <remarks>Implements PRIV-RIGHT-001a.</remarks>
internal sealed record ObjectionView(
    string Purpose,
    string Document,
    string NoticeVersion,
    ConsentMechanism Mechanism,
    DateTimeOffset RecordedAt,
    DateTimeOffset? WithdrawnAt);
