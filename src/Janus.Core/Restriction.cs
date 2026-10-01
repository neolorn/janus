using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// A named restriction on sending: what it counts against, which sends it applies to,
/// and the allowances it holds them to.
/// </summary>
/// <param name="Name">The name the management application edits it under.</param>
/// <param name="Key">What the restriction counts against.</param>
/// <param name="HostKeyName">
/// The name after the colon of a host-supplied key, and absent for every other kind.
/// A restriction naming one with no registered supplier fails startup.
/// </param>
/// <param name="Purpose">Which sends it applies to.</param>
/// <param name="Buckets">The allowances, all of which a send has to satisfy.</param>
/// <remarks>Implements chapter 10 sections 5.14 to 5.16 and 5.15a, AUTH-ABUSE-004.</remarks>
public sealed record Restriction(
    string Name,
    RestrictionKeyKind Key,
    string? HostKeyName,
    RestrictionPurpose Purpose,
    IReadOnlyList<Bucket> Buckets)
{
    /// <summary>
    /// Which channel's sends it governs; <see cref="RestrictionChannel.Any"/>, either,
    /// where it names none.
    /// </summary>
    public RestrictionChannel Channel { get; init; } = RestrictionChannel.Any;
}
