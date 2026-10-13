using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.SignIn;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.SignIn;

/// <summary>
/// The sign-ins in progress, over the <c>signin_challenges</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="ring">The key ring the keys are borrowed from at each use.</param>
/// <remarks>
/// Implements AUTH-FACT-016, OPS-SEC-003 and CONV-DESIGN-003. The catalogue entries
/// accepted so far are held under the spellings of chapter 10, so the column and the
/// wire cannot drift apart (CONV-ENUM-001). The identifier's hash was computed under
/// the current version of the fingerprint key, which is the version written beside it,
/// so the rotation forgets it with the version.
/// </remarks>
internal sealed class ChallengeStore(StoreContext context, IKeyRing ring) : IChallengeStore
{
    /// <inheritdoc/>
    public async ValueTask<Challenge?> FindAsync(
        byte[] fingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);

        ChallengeRecord? record = await context.SignInChallenges
            .FindAsync([fingerprint], cancellationToken)
            .ConfigureAwait(false);

        return Read(record);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    public async ValueTask<Challenge?> FindForUpdateAsync(
        byte[] fingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);

        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("A sign-in's row is held only inside the operation's transaction.");
        }

        bool tracked = context.SignInChallenges.Local.Any(record => CryptographicOperations.FixedTimeEquals(record.Handle, fingerprint));

        ChallengeRecord? held = (await context.SignInChallenges
                .FromSql($"SELECT * FROM identity.signin_challenges WHERE handle = {fingerprint} FOR UPDATE")
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .SingleOrDefault();

        // A row the context already tracks was read before the lock, so it is read again:
        // what the decision is made on is the row as it stood when the lock was taken.
        if (held is not null && tracked)
        {
            await context.Entry(held).ReloadAsync(cancellationToken).ConfigureAwait(false);
        }

        return Read(held);
    }

    /// <inheritdoc/>
    public async ValueTask AddAsync(Challenge challenge, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        await context.SignInChallenges
            .AddAsync(
                new ChallengeRecord
                {
                    Handle = challenge.Fingerprint,
                    Subject = challenge.Subject,
                    Email = challenge.Email,
                    Identifier = challenge.Identifier,
                    FingerprintVersion = Fingerprint.CurrentVersion(ring),
                    WebAuthn = challenge.WebAuthn,
                    CreatedAt = challenge.CreatedAt,
                    ExpiresAt = challenge.ExpiresAt,
                    Presented = Spellings(challenge),
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask RecordAsync(Challenge challenge, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        ChallengeRecord record = await context.SignInChallenges
            .FindAsync([challenge.Fingerprint], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The sign-in has no row to carry the change.");

        record.Presented = Spellings(challenge);
    }

    /// <inheritdoc/>
    public async ValueTask RemoveAsync(byte[] fingerprint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);

        ChallengeRecord? record = await context.SignInChallenges
            .FindAsync([fingerprint], cancellationToken)
            .ConfigureAwait(false);

        if (record is not null)
        {
            context.SignInChallenges.Remove(record);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        await context.SignInChallenges
            .Where(challenge => challenge.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

    private static Challenge? Read(ChallengeRecord? record) =>
        record is null
            ? null
            : Challenge.Existing(
                record.Handle,
                record.Subject,
                record.Email,
                record.Identifier,
                record.WebAuthn,
                record.CreatedAt,
                record.ExpiresAt,
                [.. record.Presented.Select(VocabularyConverter<Factor>.Read)]);

    private static string[] Spellings(Challenge challenge) =>
        [.. challenge.Presented.Select(VocabularyConverter<Factor>.Write)];
}
