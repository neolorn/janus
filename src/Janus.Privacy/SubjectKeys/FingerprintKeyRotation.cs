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
/// The fingerprint key's rotation: every stored fingerprint under a previous version is
/// computed again under the current one, in batches that each commit with the progress
/// they make, and the previous versions are retired once the escrow copy of the current
/// one is sealed and no fingerprint still read stands under them.
/// </summary>
/// <param name="rotations">The progress, and whether the credential is the maintenance one.</param>
/// <param name="store">The stored fingerprints.</param>
/// <param name="work">The transaction each batch commits in.</param>
/// <param name="audit">Where each step is recorded.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <param name="fingerprintKeys">The versions the command was handed, the new one current.</param>
/// <remarks>
/// Implements OPS-SEC-003 AC6, PRIV-RIGHT-005c and IDN-PRIN-001, in the shape of the
/// key-encryption key's rotation, as entry 318 of the decisions pending review settles
/// it. The run needs nothing of the application: it reads the versions from the
/// command, runs under the maintenance credential, and resumes from the progress row
/// after a crash, a restart or a lost connection. The application looks a fingerprint
/// up under every version it holds, so the previous version stays usable until the
/// retirement.
/// </remarks>
internal sealed class FingerprintKeyRotation(
    IKeyRotationStore rotations,
    IFingerprintRotationStore store,
    IUnitOfWork work,
    IPrivacyAudit audit,
    TimeProvider time,
    FingerprintKeys fingerprintKeys)
{
    private const KeyRotationKind Kind = KeyRotationKind.FingerprintKey;

    private static readonly SystemPrincipal Principal =
        SystemPrincipal.ForDeployment("rotate-fingerprint-key", "OPS-SEC-003", SystemOperation.KeyRotation);

    private static readonly JsonSerializerOptions Spelled =
        new() { Converters = { new JsonStringEnumConverter() } };

    /// <summary>
    /// Starts the rotation to the current version, or resumes the one that stopped, and
    /// runs it until every fingerprint that can be computed again is under that version.
    /// </summary>
    /// <param name="cancellationToken">
    /// Abandons the run; the batch in hand rolls back and the next run resumes after the
    /// last one that committed.
    /// </param>
    /// <returns>
    /// The rotation, complete, or the failure naming what was refused: the credential
    /// where it is not the maintenance credential, the keys where the current version is
    /// not a new one or a fingerprint still read is under a version the command was not
    /// handed.
    /// </returns>
    public async ValueTask<Result<KeyRotationProgress>> RecomputeAsync(CancellationToken cancellationToken)
    {
        if (!await rotations.UnderMaintenanceCredentialAsync(cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<KeyRotationProgress>(Error.From(ErrorCodes.Denied));
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        DateTimeOffset now = time.GetUtcNow();
        KeyRotationProgress? latest = await rotations.LatestAsync(Kind, cancellationToken).ConfigureAwait(false);
        IReadOnlySet<int> computing = await store.FingerprintVersionsAsync(now, cancellationToken).ConfigureAwait(false);

        // As the key-encryption key's: a rotation is to a version later than any rotated
        // to before, one that stopped is finished before another starts, and every
        // fingerprint still read must be under a version the command holds and none
        // under one later than the current.
        bool resumed = latest is { RetiredAt: null } && latest.Version == fingerprintKeys.CurrentVersion;

        if ((!resumed && latest is not null && (latest.RetiredAt is null || latest.Version >= fingerprintKeys.CurrentVersion))
            || computing.Any(version => version > fingerprintKeys.CurrentVersion || !fingerprintKeys.Versions.ContainsKey(version)))
        {
            return Result.Failure<KeyRotationProgress>(KeysUnavailable());
        }

        KeyRotationProgress progress;

        if (resumed)
        {
            progress = latest!;
        }
        else
        {
            progress = KeyRotationProgress.Started(Kind, fingerprintKeys.CurrentVersion, now);
            await rotations.AddAsync(progress, cancellationToken).ConfigureAwait(false);
        }

        await RecordedAsync(
                resumed ? AuditActions.KeyRotationResumed : AuditActions.KeyRotationStarted,
                progress,
                now,
                retired: null,
                cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        if (progress.CompletedAt is null)
        {
            await PassAsync(progress, cancellationToken).ConfigureAwait(false);
        }

        await SweepAsync(progress, cancellationToken).ConfigureAwait(false);

        if (progress.CompletedAt is null)
        {
            await work.BeginAsync(cancellationToken).ConfigureAwait(false);

            DateTimeOffset completed = time.GetUtcNow();
            progress.Complete(completed);

            await rotations.RecordAsync(progress, cancellationToken).ConfigureAwait(false);
            await RecordedAsync(AuditActions.KeyRotationCompleted, progress, completed, retired: null, cancellationToken)
                .ConfigureAwait(false);
            await work.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(progress);
    }

    /// <summary>
    /// Retires the versions before the current one, once the operator has confirmed the
    /// escrow copy of the current one sealed and no fingerprint still read stands under
    /// them.
    /// </summary>
    /// <param name="cancellationToken">Abandons the retirement, which then records nothing.</param>
    /// <returns>
    /// The rotation, retired, and the versions it retired; or the failure naming what was
    /// refused: the credential where it is not the maintenance credential, the keys where
    /// the command was not handed the version of the rotation standing, and the seal
    /// where the rotation has not completed or fingerprints still read stand under a
    /// previous version.
    /// </returns>
    public async ValueTask<Result<KeyRetirement>> RetireAsync(CancellationToken cancellationToken)
    {
        if (!await rotations.UnderMaintenanceCredentialAsync(cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<KeyRetirement>(Error.From(ErrorCodes.Denied));
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        KeyRotationProgress? latest = await rotations.LatestAsync(Kind, cancellationToken).ConfigureAwait(false);

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        if (latest is not null && latest.Version != fingerprintKeys.CurrentVersion)
        {
            return Result.Failure<KeyRetirement>(KeysUnavailable());
        }

        // OPS-SEC-003 AC4: the seal is confirmed for a copy the command produced, which
        // it produces only once the rotation has completed, and is confirmed once.
        if (latest is null || latest.CompletedAt is null || latest.RetiredAt is not null)
        {
            return Result.Failure<KeyRetirement>(SealRefused(pending: null));
        }

        // A fingerprint written under a previous version since the rotation completed
        // means something still writes under it; one nothing can compute again, a held
        // username above all, is read under it until it is released. Retiring the
        // version would lose both, so it waits. What the sweep finds is computed again
        // all the same.
        int swept = await SweepAsync(latest, cancellationToken).ConfigureAwait(false);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        DateTimeOffset now = time.GetUtcNow();
        int pending = swept + await store.StandingAsync(now, cancellationToken).ConfigureAwait(false);

        if (pending > 0)
        {
            await work.CommitAsync(cancellationToken).ConfigureAwait(false);

            return Result.Failure<KeyRetirement>(SealRefused(pending));
        }

        await store.ForgetAsync(now, cancellationToken).ConfigureAwait(false);

        latest.Retire(now);

        int[] retired = [.. fingerprintKeys.Versions.Keys.Where(version => version != latest.Version).Order()];

        await rotations.RecordAsync(latest, cancellationToken).ConfigureAwait(false);
        await RecordedAsync(AuditActions.KeyRotationRetired, latest, now, retired, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(new KeyRetirement(latest, retired));
    }

    private static Error KeysUnavailable() =>
        Error.From(ErrorCodes.StartupKeyUnavailable, "member", JsonSerializer.SerializeToElement("fingerprintKeys"));

    // The seal named where the rotation cannot take it, with the count of fingerprints
    // found under a previous version where that is why.
    private static Error SealRefused(int? pending)
    {
        var details = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["member"] = JsonSerializer.SerializeToElement("sealed"),
        };

        if (pending is int count)
        {
            details["pending"] = JsonSerializer.SerializeToElement(count);
        }

        return new Error(ErrorCodes.RequestMalformed, details);
    }

    // The ordered pass: the subjects in order after the last one reached, a batch to a
    // transaction, each committing with the point it reached.
    private async ValueTask PassAsync(KeyRotationProgress progress, CancellationToken cancellationToken)
    {
        while (true)
        {
            await work.BeginAsync(cancellationToken).ConfigureAwait(false);

            KeyRotationBatch batch = await store
                .RecomputeSubjectsAfterAsync(progress.LastSubject, KeyRotation.BatchSize, time.GetUtcNow(), cancellationToken)
                .ConfigureAwait(false);

            if (batch.Last is not SubjectId last)
            {
                await work.CommitAsync(cancellationToken).ConfigureAwait(false);

                return;
            }

            progress.Passed(last, batch.Processed);

            await rotations.RecordAsync(progress, cancellationToken).ConfigureAwait(false);
            await work.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    // What the ordered pass cannot reach: fingerprints written under a previous version
    // behind the point it had reached, and the mailboxes' addresses. Returns how many
    // were computed again.
    private async ValueTask<int> SweepAsync(KeyRotationProgress progress, CancellationToken cancellationToken)
    {
        int swept = 0;
        int taken;

        do
        {
            await work.BeginAsync(cancellationToken).ConfigureAwait(false);

            taken = await store
                .RecomputeRemainingAsync(KeyRotation.BatchSize, time.GetUtcNow(), cancellationToken)
                .ConfigureAwait(false);
            progress.Swept(taken);

            await rotations.RecordAsync(progress, cancellationToken).ConfigureAwait(false);
            await work.CommitAsync(cancellationToken).ConfigureAwait(false);

            swept += taken;
        }
        while (taken == KeyRotation.BatchSize);

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
}
