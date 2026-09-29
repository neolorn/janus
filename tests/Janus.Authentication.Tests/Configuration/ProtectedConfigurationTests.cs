using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Janus.Authentication.Configuration;
using Janus.Authentication.Oidc;
using Janus.Authentication.Tests.Oidc;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Configuration;

/// <summary>
/// How a change from the server is told: through the alert channels, in the change's
/// own transaction, so the event is written with the row and a change whose alert
/// cannot be raised is not made (OPS-ALERT-001, D-166 entry 308).
/// </summary>
[Trait("kind", "unit")]
public sealed class ProtectedConfigurationTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private const string Reason = "The provider's own limits now apply.";

    private readonly ConfigurationInMemory _configuration = new();
    private readonly ProtectedSettingsInMemory _settings = new();
    private readonly ConfigurationAuditInMemory _changes = new();
    private readonly EventsInMemory _events = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);

    /// <summary>
    /// A deployment that names every key it has to, with one origin.
    /// </summary>
    public ProtectedConfigurationTests()
    {
        _configuration.Set(Settings.HostingLocation, HostingLocation.Inside);
        _configuration.Set<IReadOnlyList<string>>(Settings.WebAuthnOrigins, ["https://accounts.example.test"]);
    }

    private ProtectedConfiguration Configuration =>
        new(
            _configuration,
            _settings,
            _changes,
            _events,
            new RedirectValidation(new OidcClientStoreInMemory(), _configuration),
            _work,
            _clock);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _work.DisposeAsync();

    /// <summary>
    /// OPS-ALERT-001: a protected change is raised through the alert channels as the
    /// High <c>protected-setting-changed</c> naming the key, inside the transaction
    /// that writes it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_001_AProtectedChangeIsAnnouncedAsync()
    {
        Result changed = await Configuration.ChangeAsync(
            [new ProtectedSettingValue<bool>(Settings.AbuseThrottleEnabled, false)],
            Reason,
            TestContext.Current.CancellationToken);

        AlertRaised raised = Assert.Single(_events.Of<AlertRaised>());

        Assert.Null(changed.Match(() => (Error?)null, error => error));
        Assert.Equal(AlertCondition.ProtectedSettingChanged, raised.Condition);
        Assert.Equal(AlertSeverity.High, raised.Severity);
        Assert.Equal(Settings.AbuseThrottleEnabled.Key.ToString(), raised.Details["key"].GetString());
        Assert.Equal(Settings.AbuseThrottleEnabled.Write(false), _settings.Written[Settings.AbuseThrottleEnabled.Key]);
        Assert.Equal(_work.Opened, _work.Committed);
    }

    /// <summary>
    /// OPS-ALERT-001: a change whose alert the channels cannot raise is not made; the
    /// refusal is the answer, and the transaction is not committed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_001_AProtectedChangeWhoseAlertIsNotRaisedIsNotMadeAsync()
    {
        _events.Refusal = Error.From(ErrorCodes.Denied);

        Result changed = await Configuration.ChangeAsync(
            [new ProtectedSettingValue<bool>(Settings.AbuseThrottleEnabled, false)],
            Reason,
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.Denied, changed.Match(() => (Error?)null, error => error)?.Code);
        Assert.Empty(_events.Published);
        Assert.Equal(0, _work.Committed);
    }
}
