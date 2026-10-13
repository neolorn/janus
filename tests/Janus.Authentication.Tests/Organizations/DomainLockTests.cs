using System;
using System.Threading.Tasks;
using Janus.Authentication.Organizations;
using Janus.Authentication.Tests.Policies;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Organizations;

/// <summary>
/// The lock an organization holds its members' sign-in addresses to (REG-DOM-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class DomainLockTests
{
    private const string Unparsed = "an address that does not parse";

    private static readonly OrganizationId Locked = new(Guid.NewGuid());

    private readonly MembershipLookupInMemory _memberships = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly DomainStoreInMemory _domains = new();

    private DomainLock Lock => new(_memberships, _configuration, _domains);

    /// <summary>
    /// REG-DOM-001: an address that does not parse has a domain that does not read, so
    /// every lock the account is under refuses it, and an account under no lock is
    /// refused nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_DOM_001_AnAddressThatDoesNotParseIsRefusedWhereverALockAppliesAsync()
    {
        var member = new SubjectId(Guid.NewGuid());
        var unlocked = new SubjectId(Guid.NewGuid());

        _memberships.Place(member, Locked);
        _configuration.Set(
            Settings.OrganizationPolicy,
            Locked.ToString(),
            PolicyOverride.None with { EmailDomains = ["example.test"] });

        Error? refused = await Lock.RefusedAsync(member, Unparsed, TestContext.Current.CancellationToken);
        Error? admitted = await Lock.RefusedAsync(unlocked, Unparsed, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.IdentifierDomainNotAllowed, refused?.Code);
        Assert.Null(admitted);
    }
}
