using Janus.Core;

namespace Janus.Hosting.Privacy;

/// <summary>
/// What triggers a takedown.
/// </summary>
/// <param name="Trigger">What raised the indication, spelled as chapter 10 section 5.12d spells it.</param>
/// <param name="Reason">The written reason, which is recorded.</param>
/// <remarks>Implements chapter 09 section 8a and IDN-LIFE-003.</remarks>
internal sealed record TakedownBody(TakedownTrigger? Trigger, string? Reason);
