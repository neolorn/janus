using System;

namespace HostSample;

/// <summary>
/// The guest's record a walk-in was taken under.
/// </summary>
/// <param name="Subject">The person record's subject.</param>
/// <param name="Created">Whether the record was created by this walk-in, rather than reused.</param>
public sealed record WalkedIn(Guid Subject, bool Created);
