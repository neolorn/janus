using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Tests.Organizations;

/// <summary>
/// The TXT records a test has published, answered as a resolver would. A name that
/// has none answers an empty list; a resolver taken down answers a failure.
/// </summary>
internal sealed class DnsResolverInMemory : IDnsResolver
{
    private readonly Dictionary<string, List<string>> _records = new(System.StringComparer.Ordinal);

    /// <summary>
    /// Whether every lookup fails, as one made while the resolver is unreachable does.
    /// </summary>
    public bool Unreachable { get; set; }

    /// <summary>
    /// Every name a lookup was made for, in order.
    /// </summary>
    public List<string> Asked { get; } = [];

    /// <summary>
    /// Publishes a record.
    /// </summary>
    /// <param name="name">Where.</param>
    /// <param name="value">What it carries.</param>
    public void Publish(string name, string value)
    {
        if (!_records.TryGetValue(name, out List<string>? held))
        {
            held = [];
            _records[name] = held;
        }

        held.Add(value);
    }

    /// <summary>
    /// Takes down every record published at a name.
    /// </summary>
    /// <param name="name">Where.</param>
    public void Withdraw(string name) => _records.Remove(name);

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<string>>> TextRecordsAsync(
        string name,
        CancellationToken cancellationToken)
    {
        Asked.Add(name);

        if (Unreachable)
        {
            return ValueTask.FromResult(Result.Failure<IReadOnlyList<string>>(Error.From(ErrorCodes.DomainUnverified)));
        }

        return ValueTask.FromResult(Result.Success<IReadOnlyList<string>>(
            _records.TryGetValue(name, out List<string>? held) ? [.. held] : []));
    }
}
