using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.SubjectKeys;

/// <summary>
/// The key-encryption key's rotation: every value wrapped under a previous version is
/// wrapped again under the current one, in batches that each commit with the progress
/// they make, and the previous versions are retired once the escrow copy of the current
/// one is sealed.
/// </summary>
/// <param name="store">The progress and the wrapped values.</param>
/// <param name="work">The transaction each batch commits in.</param>
/// <param name="audit">Where each step is recorded.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <param name="ring">The key ring the command filled with the versions it was handed, the new one current.</param>
/// <remarks>
/// Implements OPS-SEC-003, DR-009a, IDN-PRIN-001 and PRIV-RIGHT-005a, as entries 316 and
/// 317 of the decisions pending review settle them. The run needs nothing of the
/// application: it reads the versions from the command, runs under the maintenance
/// credential, and resumes from the progress row after a crash, a restart or a lost
/// connection. No stored ciphertext changes, because only the wrapping of the keys
/// does.
/// </remarks>
internal sealed class KeyRotation(
    IKeyRotationStore store,
    IUnitOfWork work,
    IPrivacyAudit audit,
    TimeProvider time,
    IKeyRing ring)
{
    /// <summary>
    /// How many subject keys one transaction takes (OPS-SEC-003, D-153).
    /// </summary>
    public const int BatchSize = 500;

    private const string Reason = "OPS-SEC-003";

    private const KeyRotationKind Kind = KeyRotationKind.KeyEncryptionKey;

    private static readonly SystemPrincipal Principal =
        SystemPrincipal.ForDeployment("rotate-kek", Reason, SystemOperation.KeyRotation);

    private static readonly JsonSerializerOptions Spelled =
        new() { Converters = { new JsonStringEnumConverter() } };

    /// <summary>
    /// Starts the rotation to the current version, or resumes the one that stopped, and
    /// runs it until every value is under that version.
    /// </summary>
    /// <param name="cancellationToken">
    /// Abandons the run; the batch in hand rolls back and the next run resumes after the
    /// last one that committed.
    /// </param>
    /// <returns>
    /// The rotation, complete, or the failure naming what was refused: the credential
    /// where it is not the maintenance credential, the keys where the current version is
    /// not a new one or a stored value is wrapped under a version the command was not
    /// handed.
    /// </returns>
    public async ValueTask<Result<KeyRotationProgress>> ReWrapAsync(CancellationToken cancellationToken)
    {
        if (!await store.UnderMaintenanceCredentialAsync(cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<KeyRotationProgress>(Error.From(ErrorCodes.Denied));
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure<KeyRotationProgress>(notBegun);
        }

        KeyRotationProgress? latest = await store.LatestAsync(Kind, cancellationToken).ConfigureAwait(false);
        IReadOnlySet<int> wrapping = await store.WrappingVersionsAsync(cancellationToken).ConfigureAwait(false);

        if (Held() is not { } held)
        {
            return Result.Failure<KeyRotationProgress>(KeysUnavailable());
        }

        // A rotation is to a version later than any rotated to before, and one that
        // stopped is finished before another starts; every value must unwrap under a
        // version the command holds, and none may be under one later than the current.
        bool resumed = latest is { RetiredAt: null } && latest.Version == held.Current;

        if ((!resumed && latest is not null && (latest.RetiredAt is null || latest.Version >= held.Current))
            || wrapping.Any(version => version > held.Current || !held.Versions.Contains(version)))
        {
            return Result.Failure<KeyRotationProgress>(KeysUnavailable());
        }

        DateTimeOffset now = time.GetUtcNow();
        KeyRotationProgress progress;

        if (resumed)
        {
            progress = latest!;
        }
        else
        {
            progress = KeyRotationProgress.Started(Kind, held.Current, now);
            await store.AddAsync(progress, cancellationToken).ConfigureAwait(false);
        }

        await RecordedAsync(
                resumed ? AuditActions.KeyRotationResumed : AuditActions.KeyRotationStarted,
                progress,
                now,
                retired: null,
                cancellationToken)
            .ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure<KeyRotationProgress>(notCommitted);
        }

        if (progress.CompletedAt is null)
        {
            await PassAsync(progress, cancellationToken).ConfigureAwait(false);
        }

        await SweepAsync(progress, cancellationToken).ConfigureAwait(false);

        if (progress.CompletedAt is null)
        {
            if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
                .Match<Error?>(() => null, error => error) is Error notBegunAgain)
            {
                return Result.Failure<KeyRotationProgress>(notBegunAgain);
            }

            DateTimeOffset completed = time.GetUtcNow();
            progress.Complete(completed);

            await store.RecordAsync(progress, cancellationToken).ConfigureAwait(false);
            await RecordedAsync(AuditActions.KeyRotationCompleted, progress, completed, retired: null, cancellationToken)
                .ConfigureAwait(false);

            if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
                .Match<Error?>(() => null, error => error) is Error notCommittedAgain)
            {
                return Result.Failure<KeyRotationProgress>(notCommittedAgain);
            }
        }

        return Result.Success(progress);
    }

    /// <summary>
    /// Retires the versions before the current one, once the operator has confirmed the
    /// escrow copy of the current one sealed.
    /// </summary>
    /// <param name="cancellationToken">Abandons the retirement, which then records nothing.</param>
    /// <returns>
    /// The rotation, retired, and the versions it retired; or the failure naming what was
    /// refused: the credential where it is not the maintenance credential, the keys where
    /// the command was not handed the version of the rotation standing, and the seal
    /// where the rotation has not completed or values are still being wrapped under a
    /// previous version.
    /// </returns>
    public async ValueTask<Result<KeyRetirement>> RetireAsync(CancellationToken cancellationToken)
    {
        if (!await store.UnderMaintenanceCredentialAsync(cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<KeyRetirement>(Error.From(ErrorCodes.Denied));
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure<KeyRetirement>(notBegun);
        }

        KeyRotationProgress? latest = await store.LatestAsync(Kind, cancellationToken).ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure<KeyRetirement>(notCommitted);
        }

        if (Held() is not { } held || (latest is not null && latest.Version != held.Current))
        {
            return Result.Failure<KeyRetirement>(KeysUnavailable());
        }

        // OPS-SEC-003 AC4: the seal is confirmed for a copy the command produced, which
        // it produces only once the rotation has completed, and is confirmed once.
        if (latest is null || latest.CompletedAt is null || latest.RetiredAt is not null)
        {
            return Result.Failure<KeyRetirement>(SealRefused(pending: null));
        }

        // A value wrapped under a previous version since the rotation completed means
        // something still wraps under it, and retiring that version would strand what it
        // wraps next. The values found are re-wrapped all the same.
        int pending = await SweepAsync(latest, cancellationToken).ConfigureAwait(false);

        if (pending > 0)
        {
            return Result.Failure<KeyRetirement>(SealRefused(pending));
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegunAgain)
        {
            return Result.Failure<KeyRetirement>(notBegunAgain);
        }

        DateTimeOffset now = time.GetUtcNow();
        latest.Retire(now);

        int[] retired = [.. held.Versions.Where(version => version != latest.Version).Order()];

        await store.RecordAsync(latest, cancellationToken).ConfigureAwait(false);
        await RecordedAsync(AuditActions.KeyRotationRetired, latest, now, retired, cancellationToken).ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommittedAgain)
        {
            return Result.Failure<KeyRetirement>(notCommittedAgain);
        }

        return Result.Success(new KeyRetirement(latest, retired));
    }

    private static Error KeysUnavailable() =>
        Error.From(ErrorCodes.StartupSecretUnavailable, "key", JsonSerializer.SerializeToElement("keyEncryptionKeys"));

    // OPS-SEC-003 AC4: the seal refused as not ready, with the count of values found
    // under a previous version where that is why.
    private static Error SealRefused(int? pending) =>
        pending is int count
            ? Error.From(ErrorCodes.RotationNotReady, "pending", JsonSerializer.SerializeToElement(count))
            : Error.From(ErrorCodes.RotationNotReady);

    // The ordered pass: the subject-key rows in order after the last one reached, a batch to a
    // transaction, each committing with the point it reached.
    private async ValueTask PassAsync(KeyRotationProgress progress, CancellationToken cancellationToken)
    {
        while (true)
        {
            (await work.BeginAsync(cancellationToken).ConfigureAwait(false))
                .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));

            KeyRotationBatch batch = await store
                .ReWrapSubjectKeysAfterAsync(progress.LastKey, BatchSize, cancellationToken)
                .ConfigureAwait(false);

            if (batch.Last is not SubjectKeyId last)
            {
                (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
                    .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));

                return;
            }

            progress.Passed(last, batch.Processed);

            await store.RecordAsync(progress, cancellationToken).ConfigureAwait(false);
            (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
                .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));
        }
    }

    // What the ordered pass cannot reach: subject keys written under a previous version
    // behind the point it had reached. Returns how many were re-wrapped.
    private async ValueTask<int> SweepAsync(KeyRotationProgress progress, CancellationToken cancellationToken)
    {
        int swept = 0;
        int taken;

        do
        {
            (await work.BeginAsync(cancellationToken).ConfigureAwait(false))
                .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));

            taken = await store.ReWrapRemainingSubjectKeysAsync(BatchSize, cancellationToken).ConfigureAwait(false);
            progress.Swept(taken);

            await store.RecordAsync(progress, cancellationToken).ConfigureAwait(false);
            (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
                .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));

            swept += taken;
        }
        while (taken == BatchSize);

        return swept;
    }

    private async ValueTask RecordedAsync(
        AuditAction action,
        KeyRotationProgress progress,
        DateTimeOffset at,
        IReadOnlyList<int>? retired,
        CancellationToken cancellationToken)
    {
        var details = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["kind"] = JsonSerializer.SerializeToElement(progress.Kind, Spelled),
            ["version"] = JsonSerializer.SerializeToElement(progress.Version),
            ["processed"] = JsonSerializer.SerializeToElement(progress.Processed),
        };

        if (retired is not null)
        {
            details["retired"] = JsonSerializer.SerializeToElement(retired);
        }

        await audit.RecordedAsync(action, Principal, subject: null, at, details, cancellationToken).ConfigureAwait(false);
    }

    // The versions the ring holds, as numbers only; a ring without the key answers none.
    private (int Current, IReadOnlySet<int> Versions)? Held() =>
        ring
            .BorrowKeyEncryptionKeys<(int Current, IReadOnlySet<int> Versions)?>(
                keys => (keys.CurrentVersion, keys.Versions.Keys.ToHashSet()))
            .Match(held => held, _ => null);
}
