using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Registration;

/// <summary>
/// The bot defence where a registration session is created: asked before anything of
/// the session exists, answered by the host's verifier where one is declared, and
/// counting each session it lets through against its source (AUTH-ABUSE-008).
/// </summary>
public sealed partial class RegistrationServiceTests
{
    private const string Datacenter = "203.0.113.9";
    private const string Solved = "solved";

    /// <summary>
    /// AUTH-ABUSE-008 AC1: an ordinary registration begins with no challenge, a
    /// verifier declared or not, and nothing is recorded about it.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_008_AC1_AnOrdinaryRegistrationBeginsWithNoChallengeAsync()
    {
        Verifying();

        _ = Ok(await BegunFromAsync(Source));

        Assert.Empty(_signalled.Records);
        _ = Assert.Single(_sessions.All);
    }

    /// <summary>
    /// AUTH-ABUSE-008 AC2 and AC3: each session created is counted against its source,
    /// so more of them from one source in an hour than the deployment admits presents
    /// the challenge, and another source is not held to that count.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_008_AC2_RepeatedSessionsFromOneSourcePresentAChallengeAsync()
    {
        _configuration.Set(Settings.AbuseBotDefenceRepeatedAttempts, 2);
        Verifying();

        _ = Ok(await BegunFromAsync(Source));
        _ = Ok(await BegunFromAsync(Source));
        _ = Ok(await BegunFromAsync(Source));

        Assert.Equal(ErrorCodes.ChallengeRequired, Refused(await BegunFromAsync(Source)));
        Assert.Equal(
            (BotDefenceSignal.RepeatedAttempts, Source, true),
            Assert.Single(_signalled.Records));
        Assert.Equal(3, _sessions.All.Count);

        _ = Ok(await BegunFromAsync("198.51.100.8"));

        _clock.Advance(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(1));

        _ = Ok(await BegunFromAsync(Source));
    }

    /// <summary>
    /// AUTH-ABUSE-008 AC2: a refused request creates no session, so it is not counted
    /// against its source either.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_008_AC2_ARefusedRequestIsNotCountedAgainstItsSourceAsync()
    {
        Verifying();
        _ranges.Inside.Add(Datacenter);

        Assert.Equal(ErrorCodes.ChallengeRequired, Refused(await BegunFromAsync(Datacenter)));
        Assert.Equal(ErrorCodes.ChallengeRequired, Refused(await BegunFromAsync(Datacenter)));

        Assert.Equal(
            0,
            await _sources.SinceAsync(Datacenter, Noon, TestContext.Current.CancellationToken));

        _ = Ok(await BegunFromAsync(Datacenter, Solved));

        Assert.Equal(
            1,
            await _sources.SinceAsync(Datacenter, Noon, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-ABUSE-008 AC4: with a verifier declared, a signal answers
    /// <c>auth.challenge.required</c> and creates no session until the request is
    /// repeated with a token the verifier passes.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_008_AC4_ASignalWithAVerifierCreatesNoSessionUntilAPassingTokenAsync()
    {
        Verifying();
        _ranges.Inside.Add(Datacenter);

        Assert.Equal(ErrorCodes.ChallengeRequired, Refused(await BegunFromAsync(Datacenter)));
        Assert.Equal(ErrorCodes.ChallengeRequired, Refused(await BegunFromAsync(Datacenter, "guessed")));
        Assert.Empty(_sessions.All);

        RegistrationSessionId session = Ok(await BegunFromAsync(Datacenter, Solved));

        Assert.Equal(session, Assert.Single(_sessions.All).Id);
    }

    /// <summary>
    /// AUTH-ABUSE-008 AC4: with no verifier declared the signal is recorded and the
    /// session created, whatever token the request carries.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_008_AC4_WithNoVerifierTheSignalIsRecordedAndTheSessionCreatedAsync()
    {
        _ranges.Inside.Add(Datacenter);

        _ = Ok(await BegunFromAsync(Datacenter));
        _ = Ok(await BegunFromAsync(Datacenter, "unasked"));

        Assert.Equal(
            [
                (BotDefenceSignal.DatacenterRange, Datacenter, false),
                (BotDefenceSignal.DatacenterRange, Datacenter, false),
            ],
            _signalled.Records);
        Assert.Equal(2, _sessions.All.Count);
    }

    /// <summary>
    /// AUTH-ABUSE-008 AC5: at the begin of a registration the signal's record is
    /// committed, alone, before the verifier is asked, no unit of work is open while it
    /// is asked, nothing of the session exists by then, and the record stands where the
    /// verifier then requires the challenge.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_008_AC5_TheRecordIsCommittedAndNoUnitOfWorkIsOpenWhenTheVerifierIsAskedAsync()
    {
        var asked = new List<(int Recorded, int Committed, bool Open, int Sessions)>();

        _verifier = new ChallengeVerifier((token, _) =>
        {
            asked.Add((_signalled.Records.Count, _work.OutermostCommitted, _work.Open, _sessions.All.Count));

            return ValueTask.FromResult(string.Equals(token, Solved, StringComparison.Ordinal));
        });
        _ranges.Inside.Add(Datacenter);

        Assert.Equal(ErrorCodes.ChallengeRequired, Refused(await BegunFromAsync(Datacenter, "guessed")));

        Assert.Equal((1, 1, false, 0), Assert.Single(asked));
        Assert.Equal((1, 0, false), (_work.OutermostCommitted, _work.RolledBack, _work.Open));
        Assert.Equal(
            (BotDefenceSignal.DatacenterRange, Datacenter, true),
            Assert.Single(_signalled.Records));

        _ = Ok(await BegunFromAsync(Datacenter, Solved));

        Assert.Equal([(1, 1, false, 0), (2, 2, false, 0)], asked);
        Assert.Equal((3, 0, false), (_work.OutermostCommitted, _work.RolledBack, _work.Open));
    }

    /// <summary>
    /// A challenge that is owed creates nothing: the invitation whose link the request
    /// presented stays unopened, and the passing repeat opens it (AUTH-ABUSE-008,
    /// REG-INV-001).
    /// </summary>
    [Fact]
    public async Task BeginAsync_AChallengeOwedWithAnInvitationToken_LeavesTheInvitationUnopenedAsync()
    {
        Verifying();
        _ranges.Inside.Add(Datacenter);

        string token = Issued(email: Address);

        Assert.Equal(
            ErrorCodes.ChallengeRequired,
            Refused(await BegunFromAsync(Datacenter, challengeToken: null, invitationToken: token)));
        Assert.True(_invitations.Held.Single().Opens(_clock.GetUtcNow()));

        RegistrationSessionId session = Ok(await BegunFromAsync(Datacenter, Solved, token));

        Assert.Equal(session, _invitations.Held.Single().Session);
    }

    /// <summary>
    /// A person signed in already is refused before the defence is asked, since no
    /// session is created for them: nothing is recorded and the verifier is not called
    /// (REG-SESS-002).
    /// </summary>
    [Fact]
    public async Task BeginAsync_SignedInFromASignalledSource_IsRefusedWithoutAskingTheDefenceAsync()
    {
        int asked = 0;

        _verifier = new ChallengeVerifier((_, _) =>
        {
            asked++;

            return ValueTask.FromResult(false);
        });
        _ranges.Inside.Add(Datacenter);

        Result<RegistrationSessionId> refused = await Service.BeginAsync(
            AccessContext.Of(SubjectId.New(_randomness)),
            Client,
            Language,
            Datacenter,
            Datacenter,
            invitationToken: null,
            challengeToken: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.RegistrationSignedIn, Refused(refused));
        Assert.Equal(0, asked);
        Assert.Empty(_signalled.Records);
    }

    private void Verifying() =>
        _verifier = new ChallengeVerifier((token, _) =>
            ValueTask.FromResult(string.Equals(token, Solved, StringComparison.Ordinal)));

    private async Task<Result<RegistrationSessionId>> BegunFromAsync(
        string source,
        string? challengeToken = null,
        string? invitationToken = null) =>
        await Service.BeginAsync(
            signedIn: null,
            Client,
            Language,
            source,
            source,
            invitationToken,
            challengeToken,
            TestContext.Current.CancellationToken);
}
