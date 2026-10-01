using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Events;
using Janus.Authentication.Factors;
using Janus.Core;
using Janus.Storage.Authentication.Events;
using Janus.Storage.Authentication.Factors;
using Janus.Storage.Settings;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Catalogue = Janus.Core.Configuration.Settings;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// The browsers an account knows, as the <c>devices</c> table holds them: a
/// fingerprint of the token and never the token (AUTH-FACT-015, AUTH-FACT-016).
/// </summary>
[Trait("kind", "integration")]
public sealed class DeviceStoreTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// The row carries what the cookie fingerprints to, so a dump of the table hands
    /// nobody a token they can present.
    /// </summary>
    [Fact]
    public async Task AddAsync_ATrustedBrowser_WritesTheFingerprintAndNotTheTokenAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        var token = OpaqueToken.Draw(_deployment.Randomness);
        Device device = Known(subject, DeviceKind.Trusted);

        await WrittenAsync(device, token);

        await using StoreContext reading = database.Context();
        DeviceRecord stored = await reading.Devices
            .SingleAsync(held => held.Id == device.Id, TestContext.Current.CancellationToken);

        Assert.Equal(Fingerprint.Length, stored.TokenFingerprint.Length);
        Assert.Equal(
            -1,
            stored.TokenFingerprint.AsSpan().IndexOf(Encoding.UTF8.GetBytes(token.Value)));

        Device found = Assert.IsType<Device>(await Store(reading)
            .FindByFingerprintAsync(token.Fingerprint(), TestContext.Current.CancellationToken));

        Assert.Equal(device.Id, found.Id);
        Assert.Equal(DeviceKind.Trusted, found.Kind);
        Assert.Equal("this laptop", found.Label.Value);
    }

    /// <summary>
    /// AUTH-FACT-015 AC6, CONV-DESIGN-003 AC6: failed sign-ins on one trusted browser made at
    /// once are counted as failures made one after another on its row, so as many as
    /// <c>factor.trusteddevice.failurelimit</c> revoke its trust.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_015_AC6_FailuresAtOnceAreAllCountedAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        var token = OpaqueToken.Draw(_deployment.Randomness);
        Device device = Known(
            subject,
            DeviceKind.Trusted,
            lifetime: DateTimeOffset.UtcNow - Noon + TimeSpan.FromDays(30));
        int limit = Catalogue.FactorTrustedDeviceFailureLimit.Default;

        await WrittenAsync(device, token);
        await Task.WhenAll(Enumerable.Range(0, limit).Select(_ => FailedAsync(subject, token)));

        await using StoreContext reading = database.Context();
        Device read = Assert.IsType<Device>(
            await Store(reading).FindAsync(device.Id, TestContext.Current.CancellationToken));

        Assert.Equal(limit, read.ConsecutiveFailures);
        Assert.True(read.Revoked);
    }

    /// <summary>
    /// What a browser did is carried onto its row: when it was last seen, how many
    /// sign-ins on it failed in a row, and whether its trust was revoked.
    /// </summary>
    [Fact]
    public async Task RecordAsync_AFailedThenRevokedBrowser_CarriesBothOntoTheRowAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        Device device = Known(subject, DeviceKind.Trusted);

        await WrittenAsync(device, OpaqueToken.Draw(_deployment.Randomness));

        await using (StoreContext changing = database.Context())
        {
            Device held = Assert.IsType<Device>(
                await Store(changing).FindAsync(device.Id, TestContext.Current.CancellationToken));

            held.Used(Noon + TimeSpan.FromDays(1));
            held.Failed(limit: 3);
            held.Failed(limit: 3);

            await Store(changing).RecordAsync(held, TestContext.Current.CancellationToken);
            await changing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();
        Device read = Assert.IsType<Device>(
            await Store(reading).FindAsync(device.Id, TestContext.Current.CancellationToken));

        Assert.Equal(Noon + TimeSpan.FromDays(1), read.LastUsedAt);
        Assert.Equal(2, read.ConsecutiveFailures);
        Assert.False(read.Revoked);
    }

    /// <summary>
    /// A browser that lapsed and one that was revoked are both absent from what the
    /// account is shown, which is the list of browsers that still stand for something.
    /// </summary>
    [Fact]
    public async Task StandingOfAsync_LapsedAndRevokedBrowsers_AreNotListedAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        Device standing = Known(subject, DeviceKind.Trusted, "this laptop");
        Device lapsed = Known(subject, DeviceKind.Remembered, "the old phone", TimeSpan.FromDays(1));
        Device revoked = Known(subject, DeviceKind.Trusted, "the shared machine");

        await WrittenAsync(standing, OpaqueToken.Draw(_deployment.Randomness));
        await WrittenAsync(lapsed, OpaqueToken.Draw(_deployment.Randomness));
        await WrittenAsync(revoked, OpaqueToken.Draw(_deployment.Randomness));

        await using (StoreContext revoking = database.Context())
        {
            Device held = Assert.IsType<Device>(
                await Store(revoking).FindAsync(revoked.Id, TestContext.Current.CancellationToken));

            held.Revoke();

            await Store(revoking).RecordAsync(held, TestContext.Current.CancellationToken);
            await revoking.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();
        IReadOnlyList<Device> listed = await Store(reading).StandingOfAsync(
            subject,
            Noon + TimeSpan.FromDays(7),
            TestContext.Current.CancellationToken);

        Assert.Equal([standing.Id], [.. Identifiers(listed)]);
    }

    /// <summary>
    /// A token no row carries stands for no browser, which is how a token from
    /// another deployment or an expired cookie is treated.
    /// </summary>
    [Fact]
    public async Task FindByFingerprintAsync_ATokenNoRowCarries_ReadsNothingAsync()
    {
        await using StoreContext reading = database.Context();

        Assert.Null(await Store(reading).FindByFingerprintAsync(
            OpaqueToken.Draw(_deployment.Randomness).Fingerprint(),
            TestContext.Current.CancellationToken));
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private static IEnumerable<DeviceId> Identifiers(IReadOnlyList<Device> devices)
    {
        foreach (Device device in devices)
        {
            yield return device.Id;
        }
    }

    // Each failure is its own request: its own context, connection and transaction.
    private async Task FailedAsync(SubjectId subject, OpaqueToken token)
    {
        await using StoreContext context = database.Context();
        await using var work = new UnitOfWork(context);

        var browsers = new DeviceService(
            Store(context),
            new ConfigurationStore(context, new DataConnections(context)),
            work,
            new EventOutbox(new PendingEvents(context), work),
            TimeProvider.System,
            _deployment.Randomness);

        Assert.True((await browsers.FailedAsync(subject, token.Value, TestContext.Current.CancellationToken))
            .Match(() => true, _ => false));
    }

    private static Device Known(
        SubjectId subject,
        DeviceKind kind,
        string label = "this laptop",
        TimeSpan? lifetime = null) =>
        Device.Known(
            DeviceId.New(TimeProvider.System),
            subject,
            kind,
            CredentialLabel.TryParse(label, out CredentialLabel parsed)
                ? parsed
                : throw new Xunit.Sdk.XunitException("The label is one the chapter admits."),
            Noon,
            lifetime ?? TimeSpan.FromDays(30));

    private static DeviceStore Store(StoreContext context) => new(context);

    private async Task WrittenAsync(Device device, OpaqueToken token)
    {
        await using StoreContext writing = database.Context();

        await Store(writing).AddAsync(
            device,
            token.Fingerprint(),
            TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
