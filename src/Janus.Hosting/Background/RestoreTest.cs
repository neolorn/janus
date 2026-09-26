using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Identity.Identifiers;
using Janus.Identity.Profiles;
using Janus.Privacy;
using Janus.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Janus.Hosting.Background;

/// <summary>
/// The automated restore test: the latest backup restored into a throwaway instance, the
/// canary subject's field decrypted there with the live keys and its account found by its
/// verified email, the time taken recorded against the objective, and the instance torn
/// down, with anything short of a pass raised.
/// </summary>
/// <param name="instance">What restores into a throwaway instance, where the deployment registered it.</param>
/// <param name="keyEncryptionKeys">The keys this process runs on, which the restored fields are opened with.</param>
/// <param name="fingerprintKeys">The keys this process runs on, which the restored identifiers are found with.</param>
/// <param name="configuration">Where the objective and the canary are read.</param>
/// <param name="audit">Where each run is recorded.</param>
/// <param name="alerts">Where a failed run goes.</param>
/// <param name="work">The transaction the record and the alert share.</param>
/// <param name="time">The clock the run is timed and stamped by.</param>
/// <param name="log">Where a step that threw is written down, by the type of what it threw.</param>
/// <remarks>
/// Implements DR-007, DR-008 and OPS-ALERT-001, as entry 334 of the decisions pending
/// review settles them. No person runs it and no person watches it. The restored
/// database is opened on its own, so nothing the test reads comes from the running one,
/// and nothing is written to the running one but the run's record and its alert. The
/// test is abandoned once the objective has passed, since it has failed by then and a
/// restore that hangs would otherwise never be raised; the instance is torn down
/// whatever became of the test, the worker stopping included.
/// </remarks>
internal sealed class RestoreTest(
    IRestoreTestInstance? instance,
    KeyEncryptionKeys keyEncryptionKeys,
    FingerprintKeys fingerprintKeys,
    IConfigurationStore configuration,
    IPrivacyAudit audit,
    IAlertChannels alerts,
    IUnitOfWork work,
    TimeProvider time,
    ILogger<RestoreTest> log)
{
    // What a failed teardown is written down as, beside the outcomes a test step fails as.
    private const string Outlived = "outlived";

    private static readonly JsonSerializerOptions Spelled =
        new() { Converters = { new JsonStringEnumConverter() } };

    /// <summary>
    /// Runs the test once.
    /// </summary>
    /// <param name="context">The principal the test runs as, which may monitor.</param>
    /// <param name="cancellationToken">
    /// Abandons the test, which is the worker stopping; the instance is still torn down.
    /// </param>
    /// <returns>Nothing, or the failure where the run could not be recorded or raised.</returns>
    /// <exception cref="ArgumentException">The context is not a principal that may monitor.</exception>
    public async ValueTask<Result> RunAsync(AccessContext context, CancellationToken cancellationToken)
    {
        SystemPrincipal principal = Monitoring(context);

        // An unreadable objective is the default's, which is also its ceiling, so the test
        // is never judged more loosely than the chapter allows.
        TimeSpan objective = (await configuration
                .ReadAsync(Settings.BackupRestoreTestObjective, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, _ => Settings.BackupRestoreTestObjective.Default);

        SubjectId? canary = (await configuration
                .ReadAsync(Settings.BackupRestoreTestCanary, cancellationToken)
                .ConfigureAwait(false))
            .Match(Canary, _ => null);

        RestoreTestOutcome outcome = RestoreTestOutcome.Unrestored;
        TimeSpan elapsed = TimeSpan.Zero;
        bool outlived = false;

        if (instance is not null)
        {
            using var deadline = new CancellationTokenSource(objective, time);
            using var within = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);

            long started = time.GetTimestamp();

            try
            {
                outcome = await TestedAsync(instance, canary, within.Token, cancellationToken).ConfigureAwait(false);
                elapsed = time.GetElapsedTime(started);
            }
            finally
            {
                outlived = !await TornDownAsync(instance, cancellationToken).ConfigureAwait(false);
            }

            // DR-007: whatever step the objective cut short, the test failed by running
            // out of time.
            if (elapsed > objective)
            {
                outcome = RestoreTestOutcome.Overrun;
            }
        }

        return await RecordedAsync(principal, Measured(outcome, elapsed, objective, outlived), outcome, outlived, cancellationToken)
            .ConfigureAwait(false);
    }

    // The setting bootstrap wrote; one that names no subject leaves nothing to decrypt.
    private static SubjectId? Canary(string written) =>
        Guid.TryParseExact(written, "D", out Guid subject) ? new SubjectId(subject) : null;

    // INF-BG-002 AC1: the test runs as a named principal that may read the state of what
    // the deployment depends on, and never as nobody.
    private static SystemPrincipal Monitoring(AccessContext context) =>
        context?.Principal is { } principal && principal.MayRun(SystemOperation.Monitoring)
            ? principal
            : throw new ArgumentException(
                "The test runs as a system principal that may monitor.",
                nameof(context));

    // The address is found as sign-in finds it: the canonical form of the canary's
    // verified email looked up by its fingerprint, which has to name the canary.
    private static async ValueTask<Result<bool>> ResolvedAsync(
        IIdentifierStore identifiers,
        SubjectId canary,
        CancellationToken cancellationToken)
    {
        IdentifierSet held = await identifiers.FindBySubjectAsync(canary, cancellationToken).ConfigureAwait(false);

        if (held.All.FirstOrDefault(identifier => identifier.Kind is IdentifierKind.Email && identifier.VerifiedAt is not null)
            is not Identifier email)
        {
            return Result.Success(false);
        }

        return Result.Success(
            await identifiers.FindHolderAsync(IdentifierKind.Email, email.Canonical, cancellationToken).ConfigureAwait(false)
                is { } holder && holder.Subject == canary);
    }

    private static string Spelling(RestoreTestOutcome outcome) =>
        JsonSerializer.SerializeToElement(outcome, Spelled).GetString() ?? string.Empty;

    private static Dictionary<string, JsonElement> Measured(
        RestoreTestOutcome outcome,
        TimeSpan elapsed,
        TimeSpan objective,
        bool outlived) =>
        new(capacity: 4, StringComparer.Ordinal)
        {
            ["outcome"] = JsonSerializer.SerializeToElement(outcome, Spelled),
            ["elapsedSeconds"] = JsonSerializer.SerializeToElement(elapsed.TotalSeconds),
            ["objectiveSeconds"] = JsonSerializer.SerializeToElement(objective.TotalSeconds),
            ["outlived"] = JsonSerializer.SerializeToElement(outlived),
        };

    // AUTH-PRIN-001 AC3: a step that throws is a step that failed, written down by the
    // type of what it threw and by what its failure is read as. A cancellation is the
    // worker's own only when the worker is stopping; one the objective threw fails the
    // step like any other fault, and the time taken judges the run.
    private async ValueTask<Result<TValue>> ContainedAsync<TValue>(
        string failedAs,
        Func<ValueTask<Result<TValue>>> step,
        CancellationToken stopping)
    {
        try
        {
            return await step().ConfigureAwait(false);
        }
        catch (Exception fault) when (fault is not OperationCanceledException || !stopping.IsCancellationRequested)
        {
            BackgroundLog.RestoreTestFaulted(log, failedAs, fault.GetType().Name);

            return Result.Failure<TValue>(Error.From(
                ErrorCodes.SystemFault,
                "fault",
                JsonSerializer.SerializeToElement(fault.GetType().Name)));
        }
    }

    // DR-008 AC2: asked whatever became of the test and never abandoned, so the worker
    // stopping does not leave the instance standing. A teardown that fails or throws is
    // an instance that may have outlived its test.
    private async ValueTask<bool> TornDownAsync(
        IRestoreTestInstance restoring,
        CancellationToken stopping) =>
        (await ContainedAsync(
                Outlived,
                async () => (await restoring.TearDownAsync(CancellationToken.None).ConfigureAwait(false))
                    .Match(() => Result.Success(true), Result.Failure<bool>),
                stopping)
            .ConfigureAwait(false))
        .Match(value => value, _ => false);

    // DR-007 AC4: each step the test gets past moves on what a failure means, so a step
    // that fails, or throws, fails the test as the step it is.
    private async ValueTask<RestoreTestOutcome> TestedAsync(
        IRestoreTestInstance restoring,
        SubjectId? canary,
        CancellationToken within,
        CancellationToken stopping)
    {
        if ((await ContainedAsync(Spelling(RestoreTestOutcome.Unrestored), () => restoring.RestoreAsync(within), stopping)
                .ConfigureAwait(false))
            .Match(value => (string?)value, _ => null) is not string restored)
        {
            return RestoreTestOutcome.Unrestored;
        }

        if (canary is not SubjectId subject
            || (await ContainedAsync(
                        Spelling(RestoreTestOutcome.Undecrypted),
                        () => ValueTask.FromResult(Result.Success(Opened(restored))),
                        stopping)
                    .ConfigureAwait(false))
                .Match(value => (ServiceProvider?)value, _ => null) is not ServiceProvider area)
        {
            return RestoreTestOutcome.Undecrypted;
        }

        await using (area)
        {
            await using AsyncServiceScope scope = area.CreateAsyncScope();

            IServiceProvider stores = scope.ServiceProvider;

            if (!(await ContainedAsync(
                        Spelling(RestoreTestOutcome.Undecrypted),
                        async () => Result.Success(
                            (await stores.GetRequiredService<IProfileStore>()
                                .FindBySubjectAsync(subject, within)
                                .ConfigureAwait(false)).DisplayName is not null),
                        stopping)
                    .ConfigureAwait(false))
                .Match(value => value, _ => false))
            {
                return RestoreTestOutcome.Undecrypted;
            }

            return (await ContainedAsync(
                        Spelling(RestoreTestOutcome.Unresolved),
                        () => ResolvedAsync(stores.GetRequiredService<IIdentifierStore>(), subject, within),
                        stopping)
                    .ConfigureAwait(false))
                .Match(value => value, _ => false)
                ? RestoreTestOutcome.Passed
                : RestoreTestOutcome.Unresolved;
        }
    }

    // DR-008 AC1: the restored database on its own, under the keys this process holds,
    // sharing no connection with the running one. Its connections are not pooled, so
    // none is left open to the instance once the test asks for it to be torn down.
    private ServiceProvider Opened(string restored)
    {
        var unpooled = new DbConnectionStringBuilder { ConnectionString = restored };
        unpooled["Pooling"] = "false";

        var services = new ServiceCollection();

        services.AddSingleton(time);
        services.AddStorageArea(unpooled.ConnectionString, keyEncryptionKeys, fingerprintKeys);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    // DR-007 AC2, AC3: every run is recorded with the time it took, and one short of a
    // pass, or whose instance may have outlived it, is raised in the same transaction.
    private async ValueTask<Result> RecordedAsync(
        SystemPrincipal principal,
        Dictionary<string, JsonElement> measured,
        RestoreTestOutcome outcome,
        bool outlived,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = time.GetUtcNow();

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await audit
            .RecordedAsync(AuditActions.RestoreTestCompleted, principal, subject: null, now, measured, cancellationToken)
            .ConfigureAwait(false);

        if ((outcome is not RestoreTestOutcome.Passed || outlived)
            && (await alerts
                    .RaiseAsync(Alerts.Of(AlertCondition.RestoreTestFailed, scope: null, now, measured), cancellationToken)
                    .ConfigureAwait(false))
                .Match(() => (Error?)null, error => error) is Error unraised)
        {
            return Result.Failure(unraised);
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
