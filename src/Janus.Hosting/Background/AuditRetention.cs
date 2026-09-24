using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Identity.Audit;
using Janus.Privacy;
using Janus.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Janus.Hosting.Background;

/// <summary>
/// The audit trail's retention: the months ahead created so the trail keeps taking rows,
/// and the partitions whose category's retention has passed dropped, under the
/// maintenance credential and with the run recorded as the job's own action.
/// </summary>
/// <param name="credential">The connection the partitions are reached over.</param>
/// <param name="keyEncryptionKeys">The keys this process runs on, which the maintenance area is built with.</param>
/// <param name="fingerprintKeys">The keys this process runs on, which the maintenance area is built with.</param>
/// <param name="configuration">Where the two retentions are read.</param>
/// <param name="audit">Where each run is recorded.</param>
/// <param name="work">The transaction the record is written in.</param>
/// <param name="time">The clock the record is stamped by.</param>
/// <remarks>
/// Implements PRIV-RET-002, OPS-MIG-003a and INF-BG-002, as entry 336 of the decisions
/// pending review settles them. The partitions are reached through a storage area of
/// their own over the maintenance credential, sharing no connection with the running
/// one; the record is written over the application's own, since the maintenance role
/// can write no row. A connection that is not the maintenance credential is refused
/// before either function is asked, and an unreadable retention drops nothing.
/// </remarks>
internal sealed class AuditRetention(
    MaintenanceCredential credential,
    KeyEncryptionKeys keyEncryptionKeys,
    FingerprintKeys fingerprintKeys,
    IConfigurationStore configuration,
    IPrivacyAudit audit,
    IUnitOfWork work,
    TimeProvider time)
{
    /// <summary>
    /// Runs the job once.
    /// </summary>
    /// <param name="context">The principal the job runs as, which may purge what is past retention.</param>
    /// <param name="cancellationToken">Abandons the run.</param>
    /// <returns>
    /// How many partitions were dropped, or the failure: the credential where it is not
    /// the maintenance credential, or a retention that could not be read.
    /// </returns>
    /// <exception cref="ArgumentException">The context is not a principal that may purge.</exception>
    public async ValueTask<Result<int>> RunAsync(AccessContext context, CancellationToken cancellationToken)
    {
        SystemPrincipal principal = Purging(context);

        await using ServiceProvider area = Opened();
        await using AsyncServiceScope scope = area.CreateAsyncScope();

        IAuditPartitions partitions = scope.ServiceProvider.GetRequiredService<IAuditPartitions>();

        if (!await partitions.UnderMaintenanceCredentialAsync(cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<int>(Error.From(ErrorCodes.Denied));
        }

        // The months ahead come first, so the trail keeps taking rows whatever becomes
        // of the drop.
        int created = await partitions.EnsureAsync(cancellationToken).ConfigureAwait(false);

        Error? failure = null;

        TimeSpan security = (await configuration
                .ReadAsync(Settings.RetentionAuditSecurity, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<TimeSpan>(error, ref failure));

        TimeSpan routine = (await configuration
                .ReadAsync(Settings.RetentionAuditRoutine, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<int>(failure);
        }

        int dropped = await partitions
            .DropExpiredAsync(security, routine, cancellationToken)
            .ConfigureAwait(false);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await audit
            .RecordedAsync(
                AuditActions.AuditPartitionsMaintained,
                principal,
                subject: null,
                time.GetUtcNow(),
                Maintained(created, dropped, security, routine),
                cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(dropped);
    }

    // INF-BG-002 AC1: the job runs as a named principal that may purge what is past its
    // retention, and never as nobody.
    private static SystemPrincipal Purging(AccessContext context) =>
        context?.Principal is { } principal && principal.MayRun(SystemOperation.RetentionPurge)
            ? principal
            : throw new ArgumentException(
                "The job runs as a system principal that may purge what is past its retention.",
                nameof(context));

    private static Dictionary<string, JsonElement> Maintained(
        int created,
        int dropped,
        TimeSpan security,
        TimeSpan routine) =>
        new(capacity: 4, StringComparer.Ordinal)
        {
            ["created"] = JsonSerializer.SerializeToElement(created),
            ["dropped"] = JsonSerializer.SerializeToElement(dropped),
            ["securityRetentionDays"] = JsonSerializer.SerializeToElement(security.TotalDays),
            ["routineRetentionDays"] = JsonSerializer.SerializeToElement(routine.TotalDays),
        };

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // OPS-MIG-003a: the maintenance credential's own area, whose connections are not
    // pooled, so none stays open under it once the run has ended.
    private ServiceProvider Opened()
    {
        var unpooled = new DbConnectionStringBuilder { ConnectionString = credential.Connection() };
        unpooled["Pooling"] = "false";

        var services = new ServiceCollection();

        services.AddSingleton(time);
        services.AddStorageArea(unpooled.ConnectionString, keyEncryptionKeys, fingerprintKeys);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}
