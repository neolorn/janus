using System;
using Janus.Core;

namespace Janus.Hosting.Privacy;

/// <summary>
/// One consent record as chapter 09 section 7 gives it.
/// </summary>
/// <param name="Purpose">Which purpose.</param>
/// <param name="NoticeVersion">The version displayed when it was given.</param>
/// <param name="Mechanism">Where it was given.</param>
/// <param name="GrantedAt">When it was given.</param>
/// <param name="WithdrawnAt">When it was taken back, where it was.</param>
/// <param name="SupersededAt">
/// When a material revision of the notice ended it, where one did. The dashboard
/// cannot know to ask again without being told, and a superseded consent prompts
/// rather than blocks (PRIV-CONS-007).
/// </param>
/// <remarks>Implements PRIV-CONS-001, PRIV-CONS-007 and PRIV-CONS-011.</remarks>
internal sealed record ConsentView(
    string Purpose,
    string NoticeVersion,
    ConsentMechanism Mechanism,
    DateTimeOffset GrantedAt,
    DateTimeOffset? WithdrawnAt,
    DateTimeOffset? SupersededAt);
