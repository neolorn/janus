using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Bootstrap;
using Janus.Authentication.Tests.Identifiers;
using Janus.Authentication.Tests.Invitations;
using Janus.Authentication.Tests.Mailboxes;
using Janus.Authentication.Tests.Organizations;
using Janus.Authentication.Tests.Policies;
using Janus.Authentication.Tests.Recovery;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Bootstrap;

/// <summary>
/// The one operation that stands a deployment up, as the unit of work sees it ended
/// (CONV-DESIGN-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class DeploymentBootstrapTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly string[] Origins = ["https://account.example.test"];

    private readonly DeploymentSeedInMemory _seed = new();
    private readonly OrganizationAuditInMemory _audit = new();
    private readonly MembershipLookupInMemory _memberships = new();
    private readonly IdentifierDirectoryInMemory _identifiers = new();
    private readonly MailboxStoreInMemory _mailboxes = new();
    private readonly RecoveryLinkStoreInMemory _links = new();
    private readonly EventsInMemory _events = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    /// <summary>
    /// A deployment that has named where its account application is served.
    /// </summary>
    public DeploymentBootstrapTests() => _configuration.Set(Settings.WebAuthnOrigins, Origins);

    private DeploymentBootstrap Bootstrap =>
        new(
            _seed,
            _audit,
            new MembershipAttachmentInMemory(_memberships),
            _identifiers,
            _mailboxes,
            _links,
            _events,
            _events,
            _configuration,
            _work,
            _randomness,
            _clock);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5, OPS-BOOT-001 AC1: a deployment that has a system
    /// administrator is refused inside the unit of work, which is rolled back with
    /// nothing committed.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_ADeploymentStoodUpAlreadyIsRolledBackAsync()
    {
        _seed.Administered = true;

        Result<BootstrapEnrolment> refused = await RunAsync(new DateOnly(1990, 1, 1));

        Assert.Equal(ErrorCodes.Denied, Refused(refused));
        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
        Assert.Empty(_seed.Accounts);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5, PRIV-MINOR-001 AC3: an under-age date is refused after the
    /// required keys were written, and the rollback leaves nothing of them committed.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_AnUnderAgeAdministratorIsRolledBackAsync()
    {
        _configuration.Set(Settings.RegistrationAdultAffirmation, AttributeRequirement.Required);

        Result<BootstrapEnrolment> refused = await RunAsync(new DateOnly(2015, 1, 1));

        Assert.Equal(ErrorCodes.ProfileUnderage, Refused(refused));
        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
        Assert.Empty(_seed.Accounts);
    }

    private static ErrorCode Refused<TValue>(Result<TValue> outcome) =>
        outcome.Match<ErrorCode>(
            _ => throw new Xunit.Sdk.XunitException("The operation was admitted."),
            error => error.Code);

    private async Task<Result<BootstrapEnrolment>> RunAsync(DateOnly dateOfBirth) =>
        await Bootstrap.RunAsync(
            new BootstrapRequest(
                "Example",
                "administrator@example.test",
                "+441632960011",
                dateOfBirth,
                Mailbox: null,
                new Dictionary<ConfigurationKey, string>()),
            TestContext.Current.CancellationToken);
}
