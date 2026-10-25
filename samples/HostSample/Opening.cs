using System;

namespace HostSample;

/// <summary>
/// A workshop an organization opens.
/// </summary>
/// <param name="Organization">The organization that runs it.</param>
/// <param name="Name">Its name.</param>
/// <param name="Capacity">How many kites it holds for repair at once.</param>
public sealed record Opening(Guid Organization, string Name, int Capacity);
