using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Search;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Janus.Hosting.Search;

/// <summary>
/// Builds the staff search's index once the host has started, keeps it current from the
/// database channel, and rebuilds it where its listening was lost or its holes passed a
/// tenth of it.
/// </summary>
/// <param name="holder">The index this process serves.</param>
/// <param name="channel">The channel every change is announced on.</param>
/// <param name="scopes">Where each step's scope comes from.</param>
/// <param name="lifetime">When the host has started.</param>
/// <param name="checks">The memory checks the process fails.</param>
/// <param name="time">The clock a pause is waited on.</param>
/// <param name="log">The host's logger.</param>
/// <remarks>
/// Implements IDN-SRCH-003 (D-256). Registered only where the host's registration
/// declares the search. Before building, the memory checks are made: a failed check
/// raises <c>search-unsafe</c>, and under <c>enforce</c> builds nothing, while under
/// <c>warn</c> it is logged at Warning and the index built anyway; past
/// <c>search.maxsubjects</c> nothing is built and <c>search-capacity</c> is raised. The
/// channel is listened on before each build, so a change committed while the index is
/// read is applied once it is swapped in. While the listening connection is lost, the
/// index in place keeps answering. A fault in a step is written down by its fault log
/// entry and the step tried again.
/// </remarks>
internal sealed class SearchIndexService(
    SearchIndexHolder holder,
    ISearchChannel channel,
    IServiceScopeFactory scopes,
    IHostApplicationLifetime lifetime,
    Func<IReadOnlyList<MemoryCheck>> checks,
    TimeProvider time,
    ILogger<SearchIndexService> log) : BackgroundService
{
    // The pause before a lost listening connection or a failed step is tried again, the
    // interval a search answered building is asked again in (IDN-SRCH-003).
    private static readonly TimeSpan Pause = TimeSpan.FromSeconds(30);

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await StartedAsync(stoppingToken).ConfigureAwait(false);

        if (!await SafeAsync(stoppingToken).ConfigureAwait(false))
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var changes = Channel.CreateUnbounded<SubjectId>();
            Task listened = await channel
                .ListenAsync(subject => changes.Writer.TryWrite(subject), stoppingToken)
                .ConfigureAwait(false);
            Built built = listened.IsCompleted ? Built.Failed : await BuiltAsync(stoppingToken).ConfigureAwait(false);

            while (!listened.IsCompleted && !stoppingToken.IsCancellationRequested)
            {
                if (built is Built.Over)
                {
                    return;
                }

                if (built is Built.Failed)
                {
                    await Task.Delay(Pause, time, stoppingToken).ConfigureAwait(false);
                    built = await BuiltAsync(stoppingToken).ConfigureAwait(false);

                    continue;
                }

                _ = await Task
                    .WhenAny(changes.Reader.WaitToReadAsync(stoppingToken).AsTask(), listened)
                    .ConfigureAwait(false);

                built = await KeptAsync(changes.Reader, stoppingToken).ConfigureAwait(false);
            }

            if (built is Built.Over)
            {
                return;
            }

            _ = changes.Writer.TryComplete();

            // The connection was lost: the index in place keeps answering, and the channel
            // is listened on again and the index rebuilt once the pause has passed.
            if (listened.Exception is AggregateException lost)
            {
                SearchLog.Failed(log, FaultLog.Of(lost.GetBaseException()));
            }

            await Task.Delay(Pause, time, stoppingToken).ConfigureAwait(false);
        }
    }

    private Task StartedAsync(CancellationToken cancellationToken)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _ = lifetime.ApplicationStarted.Register(() => started.TrySetResult());

        return started.Task.WaitAsync(cancellationToken);
    }

    // IDN-SRCH-003: what keeps memory off disk is checked before anything is read, and
    // each failed check raises search-unsafe naming it; under warn the index is built
    // anyway. A check that cannot be made, or an alert that cannot be raised, builds
    // nothing.
    private async ValueTask<bool> SafeAsync(CancellationToken cancellationToken)
    {
        bool warned = false;
        IReadOnlyList<MemoryCheck> failed = [];

        Result checkedOut = await ContainedAsync(
                async () =>
                {
                    string mode = await ReadAsync(Settings.SearchMemoryChecks, cancellationToken).ConfigureAwait(false);

                    warned = string.Equals(mode, "warn", StringComparison.Ordinal);
                    failed = checks();

                    if (failed.Count > 0 && !warned)
                    {
                        holder.Withhold(SearchUnavailability.Unsafe);
                    }

                    foreach (MemoryCheck check in failed)
                    {
                        if (warned)
                        {
                            SearchLog.CheckFailed(log, MemoryChecks.Name(check));
                        }

                        await RaiseAsync(
                                AlertCondition.SearchUnsafe,
                                new Dictionary<string, JsonElement>(capacity: 1, StringComparer.Ordinal)
                                {
                                    ["check"] = JsonSerializer.SerializeToElement(MemoryChecks.Name(check)),
                                },
                                cancellationToken)
                            .ConfigureAwait(false);
                    }

                    return Result.Success();
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (checkedOut.Match<Error?>(() => null, error => error) is Error failure)
        {
            SearchLog.Failed(log, Described(failure));
            holder.Withhold(SearchUnavailability.Unsafe);

            return false;
        }

        return failed.Count == 0 || warned;
    }

    // Applies every change heard, and rebuilds where one could not be applied or the
    // index's holes have passed a tenth of it.
    private async ValueTask<Built> KeptAsync(ChannelReader<SubjectId> changes, CancellationToken cancellationToken)
    {
        if (holder.Current().Match<SearchIndex?>(index => index, _ => null) is not SearchIndex index)
        {
            return await BuiltAsync(cancellationToken).ConfigureAwait(false);
        }

        var changed = new HashSet<SubjectId>();

        while (changes.TryRead(out SubjectId subject))
        {
            _ = changed.Add(subject);
        }

        Result applied = await ContainedAsync(
                async () =>
                {
                    AsyncServiceScope scope = scopes.CreateAsyncScope();

                    await using (scope.ConfigureAwait(false))
                    {
                        ISearchSubjects subjects = scope.ServiceProvider.GetRequiredService<ISearchSubjects>();

                        foreach (SubjectId subject in changed)
                        {
                            if (await subjects.FindAsync(subject, cancellationToken).ConfigureAwait(false)
                                is SearchSubject standing)
                            {
                                index.Put(IndexedSubject.Of(standing));
                            }
                            else
                            {
                                index.Remove(subject);
                            }
                        }
                    }

                    return Result.Success();
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (applied.Match<Error?>(() => null, error => error) is Error failure)
        {
            SearchLog.Failed(log, Described(failure));

            return await BuiltAsync(cancellationToken).ConfigureAwait(false);
        }

        return index.Fragmented ? await BuiltAsync(cancellationToken).ConfigureAwait(false) : Built.Swapped;
    }

    // IDN-SRCH-003: past search.maxsubjects nothing is built; otherwise every subject is
    // read and the index swapped in, and the build's garbage collected and compacted so
    // it goes back to the system.
    private async ValueTask<Built> BuiltAsync(CancellationToken cancellationToken)
    {
        Built built = Built.Failed;

        Result outcome = await ContainedAsync(
                async () =>
                {
                    AsyncServiceScope scope = scopes.CreateAsyncScope();

                    await using (scope.ConfigureAwait(false))
                    {
                        ISearchSubjects subjects = scope.ServiceProvider.GetRequiredService<ISearchSubjects>();
                        int most = await ReadAsync(Settings.SearchMaxSubjects, cancellationToken).ConfigureAwait(false);

                        if (await subjects.CountAsync(cancellationToken).ConfigureAwait(false) > most)
                        {
                            holder.Withhold(SearchUnavailability.Capacity);
                            built = Built.Over;

                            await RaiseAsync(AlertCondition.SearchCapacity, details: null, cancellationToken)
                                .ConfigureAwait(false);

                            return Result.Success();
                        }

                        IReadOnlyList<SearchSubject> read = await subjects.AllAsync(cancellationToken).ConfigureAwait(false);

                        holder.Swap(SearchIndex.Build([.. read.Select(IndexedSubject.Of)]));
                        built = Built.Swapped;
                    }

                    return Result.Success();
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (outcome.Match<Error?>(() => null, error => error) is Error failure)
        {
            SearchLog.Failed(log, Described(failure));
        }

        if (built is Built.Swapped)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        }

        return built;
    }

    private async ValueTask<TValue> ReadAsync<TValue>(Setting<TValue> setting, CancellationToken cancellationToken)
    {
        AsyncServiceScope scope = scopes.CreateAsyncScope();

        await using (scope.ConfigureAwait(false))
        {
            return (await scope.ServiceProvider
                    .GetRequiredService<IConfigurationStore>()
                    .ReadAsync(setting, cancellationToken)
                    .ConfigureAwait(false))
                .Match(value => value, error => throw new InvalidOperationException(error.Code.ToString()));
        }
    }

    // OPS-ALERT-001: the alert is raised in a scope and unit of work of its own, which the
    // alert raising begins and commits.
    private async ValueTask RaiseAsync(
        AlertCondition condition,
        IReadOnlyDictionary<string, JsonElement>? details,
        CancellationToken cancellationToken)
    {
        AsyncServiceScope scope = scopes.CreateAsyncScope();

        await using (scope.ConfigureAwait(false))
        {
            Result raised = await scope.ServiceProvider
                .GetRequiredService<IAlertRaising>()
                .RaiseAsync(Alerts.Of(condition, named: null, time.GetUtcNow(), details), cancellationToken)
                .ConfigureAwait(false);

            raised.Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));
        }
    }

    // What a failure is written down as: the fault log entry of what was thrown where
    // it was a fault, and its code otherwise.
    private static string Described(Error error) =>
        error.Details.TryGetValue(FaultLog.Detail, out JsonElement fault)
            ? fault.GetString() ?? string.Empty
            : error.Code.ToString();

    // A fault in a step is the step's failure, kept by its fault log entry, never by a
    // message, which can carry a value (CONV-LOG-003); the service stopping is not one.
    private static async ValueTask<Result> ContainedAsync(
        Func<ValueTask<Result>> step,
        CancellationToken cancellationToken)
    {
        try
        {
            return await step().ConfigureAwait(false);
        }
        catch (Exception fault)
            when (fault is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return Result.Failure(FaultLog.Failure(fault));
        }
    }
}
