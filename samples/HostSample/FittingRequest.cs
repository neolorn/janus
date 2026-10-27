namespace HostSample;

/// <summary>
/// A grip fitting a customer asks for in their repair.
/// </summary>
/// <param name="Need">What the customer's grip needs.</param>
public sealed record FittingRequest(string Need);
