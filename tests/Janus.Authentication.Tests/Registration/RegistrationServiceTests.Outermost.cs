using System;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Registration;

/// <summary>
/// The registration steps whose refusals keep a write, each a fault of the calling code
/// where it is called inside a unit of work another opened: it ends the level it joined
/// before it reads or writes anything else, so nothing of the outer unit of work commits
/// (CONV-DESIGN-003, AUTHZ-INHERIT-002).
/// </summary>
public sealed partial class RegistrationServiceTests
{
    /// <summary>
    /// CONV-DESIGN-003 AC8, AUTHZ-INHERIT-002: a begin, whose bot-defence signal is a
    /// kept write, called inside a unit of work another opened is a fault, and no
    /// session is created.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC8_ABeginInsideAnOpenUnitOfWorkIsAFaultAsync()
    {
        await InsideAsync(async () => await StartedAsync());

        Assert.Empty(_sessions.All);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC8, AUTHZ-INHERIT-002: an age, whose under-age refusal keeps the
    /// session's end (REG-PROF-002), recorded inside a unit of work another opened is a
    /// fault, and the screen is not locked.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC8_AnAgeInsideAnOpenUnitOfWorkIsAFaultAsync()
    {
        _configuration.Set(Settings.RegistrationAdultAffirmation, AttributeRequirement.Required);

        RegistrationSessionId session = await StartedAsync();

        await InsideAsync(async () =>
            await Service.RecordAgeAsync(session, Minor, TestContext.Current.CancellationToken));

        Assert.False(Live(session).AgeRefused);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC8, AUTHZ-INHERIT-002: a code, whose wrong try keeps its counts,
    /// presented inside a unit of work another opened is a fault, and nothing is
    /// counted.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC8_ACodeInsideAnOpenUnitOfWorkIsAFaultAsync()
    {
        RegistrationSessionId session = await AwaitingAsync();
        var unheld = new IdentifierId(Guid.NewGuid());

        await InsideAsync(async () =>
            await Service.VerifyAsync(session, unheld, "000000", Source, TestContext.Current.CancellationToken));

        Assert.Empty(_throttle.Counted);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC8, AUTHZ-INHERIT-002: a pressed token that opens nothing, whose
    /// refusal keeps its count, landed inside a unit of work another opened is a fault,
    /// and nothing is counted.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC8_APressedTokenInsideAnOpenUnitOfWorkIsAFaultAsync()
    {
        await InsideAsync(async () => await Service.LandAsync(
            session: null,
            "a-token-no-registration-sent",
            press: true,
            Source,
            TestContext.Current.CancellationToken));

        Assert.Empty(_throttle.Counted);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC8, AUTHZ-INHERIT-002: the terms, whose refusal on an address
    /// taken since it was staged keeps the session's end (REG-SESS-005), accepted inside
    /// a unit of work another opened are a fault, and the session stands.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC8_TheTermsInsideAnOpenUnitOfWorkAreAFaultAsync()
    {
        RegistrationSessionId session = await SecuredAsync();

        _directory.Held(IdentifierKind.Email, Address, SubjectId.New(_randomness));

        await InsideAsync(async () => await AcceptedAsync(session));

        Assert.Empty(_directory.Created);
        _ = Live(session);
    }

    // CONV-DESIGN-003 AC8: a step called inside a unit of work the test opened faults
    // and ends it, so the test's own commit commits nothing and faults too.
    private async Task InsideAsync(Func<Task> operation)
    {
        _work.Reset();
        _ = await _work.BeginAsync(TestContext.Current.CancellationToken);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(operation);
        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _work.CommitAsync(TestContext.Current.CancellationToken));

        Assert.False(_work.Open);
        Assert.Equal(0, _work.OutermostCommitted);
    }
}
