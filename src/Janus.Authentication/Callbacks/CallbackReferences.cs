using System;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Callbacks;

/// <summary>
/// Issues and recognises the correlation references of a host's unsigned callbacks.
/// </summary>
/// <param name="references">Where the issued references are kept, as hashes.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="randomness">Where the reference is drawn from.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements BFF-MACH-003, INT-GEN-003 and entry 276. What arrives is hashed and looked
/// up by its hash, so no comparison is ever made against the reference itself and a
/// guess learns nothing from how long the answer took.
/// </remarks>
internal sealed class CallbackReferences(
    ICallbackReferenceStore references,
    IUnitOfWork work,
    RandomNumberGenerator randomness,
    TimeProvider time) : ICallbackReferences
{
    // INT-GEN-003: 128 random bits.
    private const int Length = 16;

    /// <inheritdoc/>
    public async ValueTask<Result<string>> IssueAsync(string callback, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callback);

        byte[] drawn = new byte[Length];

        randomness.GetBytes(drawn);

        string reference = Base64Url.EncodeToString(drawn);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await references
            .AddAsync(callback, Hashed(reference), time.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(reference);
    }

    /// <summary>
    /// Whether what a callback carried is a reference issued for it.
    /// </summary>
    /// <param name="callback">The callback it arrived on.</param>
    /// <param name="presented">What it carried, where it carried anything.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether it was issued for that callback.</returns>
    /// <exception cref="ArgumentNullException">The callback is absent.</exception>
    public async ValueTask<bool> RecognisesAsync(
        string callback,
        string? presented,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(callback);

        if (string.IsNullOrEmpty(presented))
        {
            return false;
        }

        return await references
            .HoldsAsync(callback, Hashed(presented), cancellationToken)
            .ConfigureAwait(false);
    }

    private static byte[] Hashed(string reference) => SHA256.HashData(Encoding.UTF8.GetBytes(reference));
}
