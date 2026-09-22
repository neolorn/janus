using System;
using System.Linq;
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
/// <remarks>
/// Implements AUTH-FACT-016 and CONV-DESIGN-003. The catalogue entries accepted so
/// far are held under the spellings of chapter 10, so the column and the wire cannot
/// drift apart (CONV-ENUM-001).
/// </remarks>
internal sealed class ChallengeStore(JanusDbContext context) : IChallengeStore
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

        return record is null
            ? null
            : Challenge.Existing(
                record.Handle,
                record.Subject,
                record.WebAuthn,
                record.CreatedAt,
                record.ExpiresAt,
                [.. record.Presented.Select(VocabularyConverter<Factor>.Read)]);
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

    private static string[] Spellings(Challenge challenge) =>
        [.. challenge.Presented.Select(VocabularyConverter<Factor>.Write)];
}
