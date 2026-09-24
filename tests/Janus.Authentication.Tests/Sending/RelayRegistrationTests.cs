using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// Whether the deployment is warned that mail to an Apple private relay address would
/// not arrive (INT-MAIL-011).
/// </summary>
[Trait("kind", "unit")]
public sealed class RelayRegistrationTests
{
    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly ConfigurationInMemory _configuration = new();
    private readonly EventsInMemory _events = new();
    private readonly FixedClock _clock = new(Noon);

    /// <summary>
    /// A deployment sending from one domain, with the shipped policy, in which Continue
    /// with Apple is a way in.
    /// </summary>
    public RelayRegistrationTests() =>
        _configuration.Set(Settings.NotificationEmailSendingDomain, "mail.example.test");

    private RelayRegistration Relay => new(_configuration, _events, _clock);

    /// <summary>
    /// INT-MAIL-011 AC1: with Continue with Apple a way in and nothing declared as
    /// registered, the check raises the Normal condition <c>relay-domain-unregistered</c>
    /// under the domain, with the domain in its details.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task INT_MAIL_011_AC1_AnUndeclaredSendingDomainIsNamedInTheWarningAsync()
    {
        await CheckedAsync();

        AlertRaised raised = Assert.Single(_events.Of<AlertRaised>());

        Assert.Equal(AlertCondition.RelayDomainUnregistered, raised.Condition);
        Assert.Equal(AlertSeverity.Normal, raised.Severity);
        Assert.Equal("mail.example.test", raised.Details["domain"].GetString());
        Assert.StartsWith("relay-domain-unregistered:mail.example.test@", raised.IdempotencyKey, StringComparison.Ordinal);
        Assert.Equal(Noon, raised.RaisedAt);
    }

    /// <summary>
    /// A domain declared as registered, in whatever case it was written, raises nothing.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task CheckAsync_TheDomainDeclaredInAnotherCase_RaisesNothingAsync()
    {
        _configuration.Set<IReadOnlySet<string>>(
            Settings.NotificationEmailRelayRegistered,
            new HashSet<string>(["Mail.Example.Test"], StringComparer.Ordinal));

        await CheckedAsync();

        Assert.Empty(_events.Published);
    }

    /// <summary>
    /// Where Continue with Apple is no way in, no relay address can become anyone's
    /// email, and nothing is raised.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task CheckAsync_AppleIsNoWayIn_RaisesNothingAsync()
    {
        _configuration.Set(
            Settings.PolicyDefault,
            Janus.Core.Policies.SystemDefault with
            {
                LoginFactors = Janus.Core.Policies.SystemDefault.LoginFactors
                    .Where(factor => factor is not Factor.Apple)
                    .ToHashSet(),
            });

        await CheckedAsync();

        Assert.Empty(_events.Published);
    }

    /// <summary>
    /// A deployment that never named its sending domain has nothing to compare, and
    /// the check answers with the refusal the read gave.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task CheckAsync_NoSendingDomainNamed_IsRefusedAsUndeclaredAsync()
    {
        var relay = new RelayRegistration(new ConfigurationInMemory(), _events, _clock);

        Result outcome = await relay.CheckAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            ErrorCodes.StartupDeclarationMissing,
            outcome.Match<ErrorCode?>(() => null, error => error.Code));
        Assert.Empty(_events.Published);
    }

    private async Task CheckedAsync() =>
        (await Relay.CheckAsync(TestContext.Current.CancellationToken)).Switch(
            () => { },
            error => throw new Xunit.Sdk.XunitException($"The check was refused: {error.Code}."));
}
