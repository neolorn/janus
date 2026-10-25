namespace HostSample;

/// <summary>
/// A grip fitting as the people who may read it see it.
/// </summary>
/// <param name="Id">The fitting's identifier.</param>
/// <param name="Need">What the customer's grip needs.</param>
public sealed record FittingView(string Id, string Need);
