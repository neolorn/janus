using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Callbacks;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Callbacks;

/// <summary>
/// Where the correlation references of a host's unsigned callbacks are kept.
/// </summary>
/// <param name="context">The context the operation runs on.</param>
/// <remarks>Implements BFF-MACH-003, INT-GEN-003 and CONV-DESIGN-003.</remarks>
internal sealed class CallbackReferenceStore(StoreContext context) : ICallbackReferenceStore
{
    /// <inheritdoc/>
    public ValueTask AddAsync(
        string callback,
        byte[] reference,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(reference);

        context.CallbackReferences.Add(new CallbackReferenceRecord
        {
            Reference = reference,
            Callback = callback,
            IssuedAt = at,
        });

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> HoldsAsync(
        string callback,
        byte[] reference,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(reference);

        return await context.CallbackReferences
            .AnyAsync(
                issued => issued.Reference == reference && issued.Callback == callback,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
