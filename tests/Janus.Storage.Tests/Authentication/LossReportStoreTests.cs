using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Factors;
using Janus.Authentication.Recovery;
using Janus.Core;
using Janus.Storage.Authentication.Factors;
using Janus.Storage.Authentication.Recovery;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// The loss reports as the <c>loss_reports</c> table holds them, each found by its cancel
/// token's fingerprint (AUTH-RECOV-007, FE-VER-001, D-226).
/// </summary>
[Trait("kind", "integration")]
public sealed class LossReportStoreTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    /// <summary>
    /// FE-VER-001 AC8, AUTH-RECOV-007 (D-226): the link's token finds its report by the
    /// token's SHA-256 fingerprint, which the row holds beside the token encrypted for
    /// the notices that repeat it; a token no report was opened with finds none; and the
    /// cancel by the link removes the report the token found, after which the token finds
    /// nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task FE_VER_001_AC8_ALinksTokenFindsItsReportByItsFingerprintAndCancelsItAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        AuthenticatorId credential = await CredentialAsync(subject);
        var cancel = OpaqueToken.Draw(_deployment.Randomness);

        await using (StoreContext opening = database.Context())
        {
            await Store(opening).AddAsync(
                LossReport.Open(credential, subject, cancel, Noon, TimeSpan.FromHours(24)),
                TestContext.Current.CancellationToken);
            await opening.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (StoreContext reading = database.Context())
        {
            LossReportRecord held = await reading.LossReports
                .SingleAsync(report => report.Credential == credential, TestContext.Current.CancellationToken);

            Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes(cancel.Value)), held.CancelFingerprint);
            Assert.NotEqual(Encoding.UTF8.GetBytes(cancel.Value), held.Cancel);

            Assert.Null(await Store(reading).FindByCancelAsync(
                OpaqueToken.Draw(_deployment.Randomness).Fingerprint(),
                TestContext.Current.CancellationToken));
        }

        await using (StoreContext cancelling = database.Context())
        {
            LossReport found = await Store(cancelling).FindByCancelAsync(
                    OpaqueToken.Of(cancel.Value).Fingerprint(),
                    TestContext.Current.CancellationToken)
                ?? throw new Xunit.Sdk.XunitException("The link's token found no report.");

            Assert.Equal(credential, found.Credential);
            Assert.Equal(subject, found.Subject);
            Assert.Equal(Encoding.UTF8.GetBytes(cancel.Value), found.Cancel);

            await Store(cancelling).RemoveAsync(found.Credential, TestContext.Current.CancellationToken);
            await cancelling.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext after = database.Context();

        Assert.Null(await Store(after).FindByCancelAsync(cancel.Fingerprint(), TestContext.Current.CancellationToken));
    }

    private LossReportStore Store(StoreContext context) =>
        new(context, _deployment.Ring, _deployment.Randomness);

    // A credential of the account's to report lost.
    private async Task<AuthenticatorId> CredentialAsync(SubjectId subject)
    {
        var id = AuthenticatorId.New(TimeProvider.System);
        byte[] secret = new byte[20];
        _deployment.Randomness.GetBytes(secret);

        await using StoreContext writing = database.Context();

        await new AuthenticatorStore(writing, _deployment.Ring, _deployment.Randomness).AddAsync(
            Authenticator.EnrollingTotp(
                id,
                subject,
                CredentialLabel.TryParse("this phone", out CredentialLabel label)
                    ? label
                    : throw new Xunit.Sdk.XunitException("The label is one the chapter admits."),
                secret,
                Noon),
            TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        return id;
    }
}
