using System;

namespace Janus.Hosting.Records;

/// <summary>
/// A privacy notice version a person record's collection presented.
/// </summary>
/// <param name="Version">The version.</param>
/// <param name="PresentedAt">When it was presented.</param>
/// <remarks>Implements REC-ACC-002, chapter 09 section 7a.</remarks>
internal sealed record RecordNoticeView(string Version, DateTimeOffset PresentedAt);
