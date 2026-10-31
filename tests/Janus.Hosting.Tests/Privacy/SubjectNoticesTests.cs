using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Records;
using Janus.Authentication.Sending;
using Janus.Authentication.Tests.Records;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Privacy;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Janus.Hosting.Tests.Privacy;

/// <summary>
/// Where a message the privacy area needs delivered goes: through the governed send,
/// which says how many channels of the account or the record admitted it
/// (PRIV-RIGHT-002, AUTH-ABUSE-004, REC-ACC-002).
/// </summary>
[Trait("kind", "unit")]
public sealed class SubjectNoticesTests : IAsyncDisposable
{
    private const string Language = "en";

    private readonly Deployment _deployment = new();

    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    private readonly SubjectId _subject;

    /// <summary>
    /// A deployment able to send a receipt, and an account holding one verified
    /// address.
    /// </summary>
    public SubjectNoticesTests()
    {
        Flow.Prepare(_deployment);

        _subject = SubjectId.New(_randomness);

        _deployment.Accounts.Stands(_subject, AccountState.Active);
        _deployment.Identifiers.Reads(_subject, Language);
        _ = _deployment.Identifiers.Verified(_subject, IdentifierKind.Email, "subject@example.test");
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _deployment.DisposeAsync();

        _randomness.Dispose();
    }

    /// <summary>
    /// PRIV-RIGHT-002 AC1: a receipt the governed send admits is counted, which is
    /// what gives the request the instant its receipt was sent at.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_002_AC1_AReceiptTheGovernedSendAdmitsIsCountedAsync() =>
        Assert.Equal(1, await ToldAsync());

    /// <summary>
    /// PRIV-RIGHT-002 AC1, AUTH-ABUSE-004: a receipt a sending restriction refuses is
    /// counted on no channel and nothing is written for it, which is what leaves the
    /// request standing with no receipt.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_002_AC1_AReceiptARestrictionRefusesIsCountedOnNoChannelAsync()
    {
        _deployment.Configuration.Set(
            Settings.Restrictions,
            [
                .. Settings.Restrictions.Default,
                new Restriction(
                    "every.notice",
                    RestrictionKeyKind.Global,
                    null,
                    RestrictionPurpose.Notification,
                    [new Bucket(1, TimeSpan.FromHours(24), BucketWindow.Sliding)]),
            ]);

        _deployment.SendLedger.Given(
            new RestrictionKey("every.notice", RestrictionKeyKind.Global, "every.notice"),
            _deployment.Clock.GetUtcNow());

        int carried = _deployment.Mail.Taken.Count;

        Assert.Equal(0, await ToldAsync());
        Assert.Equal(carried, _deployment.Mail.Taken.Count);
    }

    /// <summary>
    /// REC-ACC-002: a message to a person record goes to its notice contacts, those
    /// verified at a collection, in the record's language, and to no other contact.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ACC_002_AMessageToARecordGoesToItsNoticeContactsAsync()
    {
        const string address = "guest@example.test";

        var record = SubjectId.New(_randomness);
        DateTimeOffset verified = _deployment.Clock.GetUtcNow();

        _deployment.Records.Hold(
            new HeldRecord(record, OrganizationId.New(_deployment.Clock), verified)
            {
                Language = Language,
                Contacts =
                {
                    [IdentifierKind.Email] = new CollectedContact(IdentifierKind.Email, address, verified),
                    [IdentifierKind.Phone] = new CollectedContact(IdentifierKind.Phone, "+441632960047", VerifiedAt: null),
                },
            });

        Assert.Equal(1, await ToldAsync(record));

        _ = await _deployment.CarrySendsAsync();

        Assert.Equal(address, Assert.Single(_deployment.Mail.Taken).Destination);
        Assert.Empty(_deployment.Sms.Taken);
    }

    // The receipt, told inside a unit of work as the request's service tells it.
    private async Task<int> ToldAsync() => await ToldAsync(_subject);

    private async Task<int> ToldAsync(SubjectId subject)
    {
        await using AsyncServiceScope scope = _deployment.Scope();

        SubjectNotices notices = ActivatorUtilities.CreateInstance<SubjectNotices>(scope.ServiceProvider);

        await _deployment.Work.BeginAsync(TestContext.Current.CancellationToken);

        int told = await notices.TellAsync(
            subject,
            MessageKind.PrivacyRequestReceived,
            source: null,
            TestContext.Current.CancellationToken);

        await _deployment.Work.CommitAsync(TestContext.Current.CancellationToken);

        return told;
    }
}
