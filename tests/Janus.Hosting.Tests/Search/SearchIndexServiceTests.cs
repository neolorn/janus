using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Search;
using Janus.Authentication.Tests;
using Janus.Authentication.Tests.Search;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Search;
using Janus.Hosting.Tests.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Janus.Hosting.Tests.Search;

/// <summary>
/// The keeping of the staff search's index in a process that serves it: built once the
/// host has started, kept current from the channel, rebuilt where the listening was lost
/// or its holes passed a tenth of it, and built not at all past its capacity or where a
/// memory check fails under <c>enforce</c> (IDN-SRCH-003, LIB-HOST-001, D-256).
/// </summary>
[Trait("kind", "unit")]
public sealed class SearchIndexServiceTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private static readonly IndexScope Everywhere = new(Organization: null, Accounts: true, Records: true);

    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();
    private readonly SearchSubjectsInMemory _subjects = new();
    private readonly SearchChannelInMemory _channel = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly EventsInMemory _alerts = new();
    private readonly HostLifetimeInMemory _lifetime = new();
    private readonly EntriesInMemory _entries = new();
    private readonly SearchIndexHolder _holder = new(served: true);
    private readonly ILoggerFactory _logging;
    private readonly ServiceProvider _services;
    private IReadOnlyList<MemoryCheck> _failed = [];
    private SearchIndexService? _service;

    /// <summary>
    /// A process's container, with the ports the index's keeping reads over fakes.
    /// </summary>
    public SearchIndexServiceTests()
    {
        _logging = LoggerFactory.Create(logging => logging.AddProvider(_entries));

        var services = new ServiceCollection();

        services.AddSingleton<ISearchSubjects>(_subjects);
        services.AddSingleton<IConfigurationStore>(_configuration);
        services.AddSingleton<IAlertRaising>(_alerts);
        _services = services.BuildServiceProvider();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_service is not null)
        {
            await _service.StopAsync(CancellationToken.None);
            _service.Dispose();
        }

        await _services.DisposeAsync();
        _holder.Dispose();
        _lifetime.Dispose();
        _logging.Dispose();
        _entries.Dispose();
        _randomness.Dispose();
    }

    /// <summary>
    /// IDN-SRCH-003 AC2: nothing is built before the host has started, the search
    /// answering <c>building</c> until then, and once it has, the index is built from the
    /// subjects held and answers them.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_SRCH_003_AC2_TheIndexIsBuiltOnceTheHostHasStartedAsync()
    {
        SubjectId held = Hold("Ahmed Hassan");

        await StartAsync(started: false);

        Assert.Equal("building", Reason());
        Assert.Equal(0, _channel.Listens);

        _lifetime.Start();

        await UntilAsync(() => Found("ahmed").Contains(held));
    }

    /// <summary>
    /// IDN-SRCH-003 AC3: a display name changed by another process changes what the next
    /// search answers once its notification is received, with no restart.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_SRCH_003_AC3_AChangeAnnouncedOnTheChannelIsAnsweredWithNoRestartAsync()
    {
        SubjectId renamed = Hold("Ahmed Hassan");

        await StartAsync();
        await UntilAsync(() => Found("ahmed").Contains(renamed));

        _subjects.Hold(Subject(renamed, "Omar Hassan"));
        _channel.Announce(renamed);

        await UntilAsync(() => Found("omar").Contains(renamed));

        Assert.DoesNotContain(renamed, Found("ahmed"));
        Assert.Equal(1, _channel.Listens);
    }

    /// <summary>
    /// IDN-SRCH-003 AC4: an erased subject is answered by no search once its notification
    /// is received.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_SRCH_003_AC4_AnErasedSubjectLeavesTheIndexOnItsNotificationAsync()
    {
        SubjectId erased = Hold("Ahmed Hassan");
        SubjectId kept = Hold("Ahmed Saleh");

        await StartAsync();
        await UntilAsync(() => Found("ahmed").Contains(erased));

        _subjects.Erase(erased);
        _channel.Announce(erased);

        await UntilAsync(() => !Found("ahmed").Contains(erased));

        Assert.Equal([kept], Found("ahmed"));
    }

    /// <summary>
    /// IDN-SRCH-003 AC5: with the listening connection lost, the search keeps answering
    /// from its index, and once the channel is listened on again, a change made while it
    /// was lost is answered by the rebuilt index swapped in.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_SRCH_003_AC5_ALostListeningKeepsAnsweringAndRebuildsAsync()
    {
        SubjectId first = Hold("Ahmed Hassan");

        await StartAsync();
        await UntilAsync(() => Found("ahmed").Contains(first));

        SearchIndex before = Index();

        _channel.Lose();

        SubjectId missed = Hold("Ahmed Saleh");

        Assert.Contains(first, Found("ahmed"));

        await UntilAsync(() => Found("ahmed").Contains(missed));

        Assert.NotSame(before, Index());
        Assert.Equal(2, _channel.Listens);
    }

    /// <summary>
    /// IDN-SRCH-003 AC6: while removed entries are a tenth of the index or fewer, the index
    /// is kept, and once they pass it, a rebuilt index is swapped in, the search answering
    /// throughout.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_SRCH_003_AC6_HolesPastATenthAreRebuiltAsync()
    {
        SubjectId[] held = [.. Enumerable.Range(0, 20).Select(at => Hold("Ahmed " + (char)('a' + at)))];

        await StartAsync();
        await UntilAsync(() => Found("ahmed").Count == 20);

        SearchIndex built = Index();

        Erase(held[0]);

        await UntilAsync(() => Found("ahmed").Count == 19);

        Assert.Same(built, Index());

        Erase(held[1]);
        Erase(held[2]);

        await UntilAsync(() => Found("ahmed").Count == 17 && !ReferenceEquals(built, Index()));

        Assert.False(Index().Fragmented);
    }

    /// <summary>
    /// IDN-SRCH-003 AC7: past <c>search.maxsubjects</c> subjects, no index is built, the
    /// search answers <c>capacity</c>, and <c>search-capacity</c> is raised.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_SRCH_003_AC7_PastTheCapacityNoIndexIsBuiltAsync()
    {
        _configuration.Set(Settings.SearchMaxSubjects, 1);
        _ = Hold("Ahmed Hassan");
        _ = Hold("Ahmed Saleh");

        await StartAsync();
        await UntilAsync(() => Reason() == "capacity");

        Assert.Equal(
            [AlertCondition.SearchCapacity],
            _alerts.Of<AlertRaised>().Select(alert => alert.Condition));
    }

    /// <summary>
    /// IDN-SRCH-003 AC8, AC10: under <c>enforce</c>, with checks failed, among them one
    /// whose source could not be read, no index is built, the search answers
    /// <c>unsafe</c>, and <c>search-unsafe</c> is raised for each check with
    /// <c>details.check</c> naming it and nothing else.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_SRCH_003_AC8_UnderEnforceAFailedCheckBuildsNothingAsync()
    {
        _failed = [MemoryCheck.Swap, MemoryCheck.Diagnostics];
        _ = Hold("Ahmed Hassan");

        await StartAsync();
        await UntilAsync(() => _alerts.Of<AlertRaised>().Count == 2);

        Assert.Equal("unsafe", Reason());
        Assert.Equal(["swap", "diagnostics"], Checks());
        Assert.All(_alerts.Of<AlertRaised>(), alert => Assert.Equal(["check"], alert.Details.Keys));
        Assert.Equal(0, _channel.Listens);
    }

    /// <summary>
    /// IDN-SRCH-003 AC8, LIB-HOST-001 AC8: under <c>warn</c>, with checks failed, the index
    /// is built and the search served, each failed check is logged at Warning, and
    /// <c>search-unsafe</c> is raised naming it; nothing the process reads says which
    /// environment it runs in.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_SRCH_003_AC8_UnderWarnAFailedCheckIsLoggedAndRaisedAndTheIndexBuiltAsync()
    {
        _configuration.Set(Settings.SearchMemoryChecks, "warn");
        _failed = [MemoryCheck.CoreDump, MemoryCheck.MiniDump];
        SubjectId held = Hold("Ahmed Hassan");

        await StartAsync();
        await UntilAsync(() => Found("ahmed").Contains(held));

        Assert.Equal(["coredump", "minidump"], Checks());
        Assert.Equal(
            ["Check=coredump", "Check=minidump"],
            _entries.Written
                .Where(entry => entry.Level == LogLevel.Warning)
                .Select(entry => entry.Line.Split(' ').Single(part => part.StartsWith("Check=", StringComparison.Ordinal))));
    }

    private async Task StartAsync(bool started = true)
    {
        _service = new SearchIndexService(
            _holder,
            _channel,
            _services.GetRequiredService<IServiceScopeFactory>(),
            _lifetime,
            () => _failed,
            new LapsedClock(Noon),
            _logging.CreateLogger<SearchIndexService>());

        await _service.StartAsync(TestContext.Current.CancellationToken);

        if (started)
        {
            _lifetime.Start();
        }
    }

    private SubjectId Hold(string name)
    {
        var subject = SubjectId.New(_randomness);

        _subjects.Hold(Subject(subject, name));

        return subject;
    }

    private void Erase(SubjectId subject)
    {
        _subjects.Erase(subject);
        _channel.Announce(subject);
    }

    private static SearchSubject Subject(SubjectId subject, string name) =>
        new(subject, SubjectKind.Account, name, [], [], []);

    private SearchIndex Index() =>
        _holder.Current().Match(index => index, error => throw new InvalidOperationException(error.Code.ToString()));

    private List<SubjectId> Found(string query) =>
        _holder.Current().Match<List<SubjectId>>(
            index => [.. index.Find([query], Everywhere, 50).Select(hit => hit.Subject)],
            _ => []);

    private string? Reason() =>
        _holder.Current().Match<string?>(
            _ => null,
            error => error.Details.TryGetValue("reason", out JsonElement reason) ? reason.GetString() : null);

    private string?[] Checks() =>
        [.. _alerts.Of<AlertRaised>().Select(alert => alert.Details["check"].GetString())];

    private static async Task UntilAsync(Func<bool> holds)
    {
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        patience.CancelAfter(Patience);

        while (!holds())
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), patience.Token);
        }
    }
}
