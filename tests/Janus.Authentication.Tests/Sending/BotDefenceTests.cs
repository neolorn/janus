using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// Bot defence at registration: nothing in front of an ordinary customer, the host's
/// challenge in front of an adverse signal, and a recorded signal where the
/// deployment declared no verifier (AUTH-ABUSE-008).
/// </summary>
[Trait("kind", "unit")]
public sealed class BotDefenceTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private const string Ordinary = "198.51.100.7";

    private const string Datacenter = "203.0.113.9";

    private readonly ConfigurationInMemory _configuration = new();
    private readonly DatacenterRangesInMemory _ranges = new();
    private readonly RegistrationSourcesInMemory _sources = new();
    private readonly BotDefenceAuditInMemory _audit = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);

    private ChallengeVerifier? _verifier;

    /// <summary>
    /// A deployment whose ranges know one address.
    /// </summary>
    public BotDefenceTests() => _ranges.Inside.Add(Datacenter);

    private BotDefence Defence =>
        new(_configuration, _ranges, _sources, _audit, _work, _verifier, _clock);

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _work.DisposeAsync();

    /// <summary>
    /// AUTH-ABUSE-008 AC1: an ordinary registration is shown nothing, and nothing is
    /// written down about it.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_008_AC1_AnOrdinaryRegistrationPresentsNoChallengeAsync()
    {
        _verifier = new ChallengeVerifier((_, _) => ValueTask.FromResult(false));

        await PassedAsync(Ordinary);

        Assert.Empty(_audit.Records);
    }

    /// <summary>
    /// AUTH-ABUSE-008 AC2: a registration from a datacenter range is shown the
    /// deployment's challenge.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_008_AC2_ADatacenterSourcePresentsAChallengeAsync()
    {
        _verifier = new ChallengeVerifier((token, _) =>
            ValueTask.FromResult(string.Equals(token, "solved", StringComparison.Ordinal)));

        Assert.Equal(ErrorCodes.ChallengeRequired, Refusal(await CheckedAsync(Datacenter, null)));
        Assert.Equal(
            (BotDefenceSignal.DatacenterRange, Datacenter, true),
            Assert.Single(_audit.Records));

        await PassedAsync(Datacenter, "solved");
    }

    /// <summary>
    /// AUTH-ABUSE-008 AC2 and AC3: the session a request would create is counted with
    /// those already created from its source in the hour, so with the default of three
    /// the fourth is shown the challenge, and a session older than the hour counts for
    /// nothing.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_008_AC3_TheSessionTheRequestWouldCreateCountsTowardRepeatedAttemptsAsync()
    {
        _verifier = new ChallengeVerifier((_, _) => ValueTask.FromResult(false));

        _sources.Given(
            Ordinary,
            Noon - TimeSpan.FromMinutes(90),
            Noon - TimeSpan.FromMinutes(20),
            Noon - TimeSpan.FromMinutes(10));

        await PassedAsync(Ordinary);
        Assert.Empty(_audit.Records);

        _sources.Given(Ordinary, Noon - TimeSpan.FromMinutes(5));

        Assert.Equal(ErrorCodes.ChallengeRequired, Refusal(await CheckedAsync(Ordinary, null)));
        Assert.Equal(
            (BotDefenceSignal.RepeatedAttempts, Ordinary, true),
            Assert.Single(_audit.Records));
    }

    /// <summary>
    /// AUTH-ABUSE-008 AC4: with no verifier declared the signal is written down and
    /// the step goes on, because the library ships no challenge to show.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_008_AC4_WithNoVerifierTheSignalIsAuditedAndNothingIsShownAsync()
    {
        await PassedAsync(Datacenter);

        Assert.Equal(
            (BotDefenceSignal.DatacenterRange, Datacenter, false),
            Assert.Single(_audit.Records));
    }

    /// <summary>
    /// AUTH-ABUSE-008 AC4: with a verifier declared the step answers
    /// <c>auth.challenge.required</c> and completes only with a token it passes.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_008_AC4_WithAVerifierOnlyAPassingTokenCompletesAsync()
    {
        _verifier = new ChallengeVerifier((token, _) =>
            ValueTask.FromResult(string.Equals(token, "solved", StringComparison.Ordinal)));

        Assert.Equal(
            ErrorCodes.ChallengeRequired,
            Refusal(await CheckedAsync(Datacenter, "guessed")));

        await PassedAsync(Datacenter, "solved");
    }


    /// <summary>
    /// AUTH-ABUSE-008 AC5: a signal that fires is recorded whether or not a verifier is
    /// declared. With one declared the record is committed, alone, before the verifier
    /// is asked, no transaction is open while it is asked, and the record stands where
    /// the verifier requires a challenge, fails or does not answer.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_008_AC5_TheSignalIsCommittedBeforeTheVerifierIsAskedAndStandsAsync()
    {
        await PassedAsync(Datacenter);

        Assert.Equal((1, 0, false), (_work.OutermostCommitted, _work.RolledBack, _work.Open));

        var asked = new List<(int Recorded, int Committed, bool Open)>();
        bool answers = true;

        _verifier = new ChallengeVerifier((_, cancellationToken) =>
        {
            asked.Add((_audit.Records.Count, _work.OutermostCommitted, _work.Open));

            return answers
                ? ValueTask.FromResult(false)
                : ValueTask.FromException<bool>(new OperationCanceledException(cancellationToken));
        });

        Assert.Equal(ErrorCodes.ChallengeRequired, Refusal(await CheckedAsync(Datacenter, null)));
        Assert.Equal(ErrorCodes.ChallengeRequired, Refusal(await CheckedAsync(Datacenter, "guessed")));

        answers = false;

        _ = await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await CheckedAsync(Datacenter, "unanswered"));

        Assert.Equal([(3, 3, false), (4, 4, false)], asked);
        Assert.Equal(
            [false, true, true, true],
            _audit.Records.Select(recorded => recorded.Challenged));
        Assert.Equal((4, 0, false), (_work.OutermostCommitted, _work.RolledBack, _work.Open));
    }
    /// <summary>
    /// A signal the deployment stopped counting fires for nobody, which is how the
    /// closed set is turned off (chapter 10 section 4.5).
    /// </summary>
    [Fact]
    public async Task CheckAsync_ASignalTheDeploymentDoesNotCount_FiresForNobodyAsync()
    {
        _configuration.Set(
            Settings.AbuseBotDefenceSignals,
            (IReadOnlySet<BotDefenceSignal>)FrozenSet<BotDefenceSignal>.Empty);

        _verifier = new ChallengeVerifier((_, _) => ValueTask.FromResult(false));

        await PassedAsync(Datacenter);

        Assert.Empty(_audit.Records);
    }

    private static ErrorCode Refusal(Result result) =>
        result.Match(
            () => throw new Xunit.Sdk.XunitException("The check was not refused."),
            error => error.Code);

    private async Task<Result> CheckedAsync(string source, string? token) =>
        await Defence.CheckAsync(source, token, TestContext.Current.CancellationToken);

    private async Task PassedAsync(string source, string? token = null) =>
        (await CheckedAsync(source, token)).Switch(
            () => { },
            error => throw new Xunit.Sdk.XunitException($"The check was refused: {error.Code}."));
}
