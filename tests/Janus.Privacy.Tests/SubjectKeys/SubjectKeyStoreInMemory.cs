using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.SubjectKeys;

namespace Janus.Privacy.Tests.SubjectKeys;

/// <summary>
/// Subject keys, in a dictionary.
/// </summary>
internal sealed class SubjectKeyStoreInMemory : ISubjectKeyStore
{
    private readonly Dictionary<SubjectKeyId, SubjectKey> _keys = [];

    /// <summary>
    /// Holds a key as a deployment's table holds one.
    /// </summary>
    /// <param name="key">The key.</param>
    public void Hold(SubjectKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        _keys[key.Id] = key;
    }

    /// <inheritdoc/>
    public ValueTask<SubjectKey?> FindBySubjectAsync(SubjectId subject, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_keys.GetValueOrDefault(SubjectKeyId.Of(subject)));

    /// <inheritdoc/>
    public ValueTask<IReadOnlySet<int>> WrappingVersionsAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlySet<int>>(
            _keys.Values.Where(key => !key.IsErased).Select(key => key.KeyVersion).ToHashSet());

    /// <inheritdoc/>
    public ValueTask AddAsync(SubjectKey key, CancellationToken cancellationToken)
    {
        Hold(key);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask CreateAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        Hold(SubjectKey.Wrapped(SubjectKeyId.Of(subject), keyVersion: 1, new byte[40]));

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RecordWrappingAsync(SubjectKey key, CancellationToken cancellationToken)
    {
        Hold(key);

        return ValueTask.CompletedTask;
    }
}
