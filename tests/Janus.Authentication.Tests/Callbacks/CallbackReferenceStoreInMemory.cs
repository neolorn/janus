using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Callbacks;

namespace Janus.Authentication.Tests.Callbacks;

/// <summary>
/// The issued correlation references, as their hashes, the way the table keeps them.
/// </summary>
internal sealed class CallbackReferenceStoreInMemory : ICallbackReferenceStore
{
    private readonly List<(string Callback, byte[] Reference)> _issued = [];

    /// <summary>
    /// Every hash kept, whatever callback it was issued for.
    /// </summary>
    public IEnumerable<byte[]> Kept => _issued.Select(issued => issued.Reference);

    /// <inheritdoc/>
    public ValueTask AddAsync(
        string callback,
        byte[] reference,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        _issued.Add((callback, reference));

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// How many times a reference was looked up.
    /// </summary>
    public int Looked { get; private set; }

    /// <inheritdoc/>
    public ValueTask<bool> HoldsAsync(string callback, byte[] reference, CancellationToken cancellationToken)
    {
        Looked++;

        return ValueTask.FromResult(_issued.Any(issued =>
            string.Equals(issued.Callback, callback, StringComparison.Ordinal)
            && issued.Reference.AsSpan().SequenceEqual(reference)));
    }
}
