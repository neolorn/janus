using Janus.Core;

namespace HostSample;

/// <summary>
/// A grip fitting the workshop makes to a kite's handles for a customer's own need, a
/// record of the customer's health held under the customer's key, and made only with
/// the customer's written consent.
/// </summary>
internal sealed class Fitting
{
    /// <summary>
    /// The fitting's identifier, under which it is registered with the library.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The repair the fitting is made in.
    /// </summary>
    public required string RepairId { get; init; }

    /// <summary>
    /// The customer the fitting is made for, whose key the need is held under.
    /// </summary>
    public required SubjectId Customer { get; init; }

    /// <summary>
    /// What the customer's grip needs, encrypted under the customer's key.
    /// </summary>
    public required byte[] Need { get; init; }
}
