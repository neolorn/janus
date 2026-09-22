using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sessions;
using Janus.Core;

namespace Janus.Authentication.Tests.Sessions;

/// <summary>
/// The local database, answering what a test put in it for an address and nothing for
/// any other, which is what a deployment holding no file answers for every one.
/// </summary>
internal sealed class LocationResolverInMemory : ILocationResolver
{
    private readonly Dictionary<string, SessionLocation> _places = [];

    /// <summary>
    /// Every address the resolver was asked about, in order.
    /// </summary>
    public List<string> Asked { get; } = [];

    /// <summary>
    /// Says where one address is.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <param name="place">Where it is.</param>
    public void Holds(string address, SessionLocation place) => _places[address] = place;

    /// <inheritdoc/>
    public ValueTask<SessionLocation?> ResolveAsync(
        string address,
        CancellationToken cancellationToken)
    {
        Asked.Add(address);

        return ValueTask.FromResult(_places.GetValueOrDefault(address));
    }
}
