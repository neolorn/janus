using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Authentication.Records;
using Janus.Authentication.Tests.Records;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Records;

/// <summary>
/// A person exercising their rights on a record attached to their account, in the
/// account's own session (REC-ATT-002).
/// </summary>
[Trait("kind", "unit")]
public sealed class AttachedRecordRightsTests : IAsyncDisposable
{
    private const string Language = "en";

    private const string Marketing = "marketing";

    private const string Security = "security";

    private static readonly RandomNumberGenerator Randomness = RandomNumberGenerator.Create();

    private readonly Deployment _deployment = new();

    private readonly OrganizationId _shop;

    private readonly SubjectId _record;

    /// <summary>
    /// A deployment able to register a browser, with one record collected in an
    /// organization.
    /// </summary>
    public AttachedRecordRightsTests()
    {
        Flow.Prepare(_deployment);
        _deployment.Configuration.Set(Settings.NotificationLanguages, [Language, "ar"]);
        _deployment.Configuration.Set(Settings.PrivacyCalendarTimeZone, "Africa/Cairo");
        _deployment.Documents.Hold(new DocumentVersion("privacy-notice", "1", "ar", "النص", [], _deployment.Clock.GetUtcNow()));

        _shop = OrganizationId.New(_deployment.Clock);
        _deployment.Organizations.Seed(_shop, name: "Seaside Rentals");

        _record = Held();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// REC-ATT-002: the account reads a record attached to it, rectifies it, changes its
    /// consents and objections and submits a request on it, each recorded for the record.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ATT_002_AnAttachedRecordsRightsAreExercisedThroughTheAccountAsync()
    {
        Browser browser = await AttachedAsync();

        Answer read = await browser.SendAsync("GET", Rights());
        Answer changed = await browser.SendAsync("PUT", Rights(), ("name", "Mona S."));
        Answer granted = await browser.SendAsync("POST", Rights("/consents/" + Marketing + "/grant"));
        Answer withdrawn = await browser.SendAsync("POST", Rights("/consents/" + Marketing + "/withdraw"));
        Answer objected = await browser.SendAsync("POST", Rights("/objections/" + Security));
        Answer received = await browser.SendAsync(
            "POST",
            Rights("/requests"),
            ("type", "restriction"),
            ("detail", "Please stop using my details."));

        Assert.Equal(StatusCodes.Status200OK, read.Status);
        Assert.Equal(_record.ToString(), read.Json().GetProperty("subject").GetString());
        Assert.Equal(
            [StatusCodes.Status204NoContent, StatusCodes.Status204NoContent, StatusCodes.Status204NoContent, StatusCodes.Status204NoContent, StatusCodes.Status202Accepted],
            new[] { changed, granted, withdrawn, objected, received }.Select(answer => answer.Status));
        Assert.Equal("Mona S.", _deployment.Records.Of(_record).Name?.Value);

        JsonElement consent = Assert.Single((await browser.SendAsync("GET", Rights("/consents"))).Json().EnumerateArray());
        JsonElement objection = Assert.Single((await browser.SendAsync("GET", Rights("/objections"))).Json().EnumerateArray());

        Assert.NotEqual(JsonValueKind.Null, consent.GetProperty("withdrawnAt").ValueKind);
        Assert.Equal(Security, objection.GetProperty("purpose").GetString());
        Assert.Equal(_record, Assert.Single(_deployment.Requests.Queue).Subject);
    }

    /// <summary>
    /// REC-ATT-002, chapter 09 section 7a: an attached record's export is the record's,
    /// counted for it, and a step-up action of the account as its own export is.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ATT_002_AnAttachedRecordsExportIsAStepUpActionAsync()
    {
        _deployment.ExportSource.Holds(
            _record,
            new ExportSection(
                "record",
                [new ExportRecord(new Dictionary<string, string>(StringComparer.Ordinal) { ["subject"] = _record.ToString() })]));

        Browser browser = await AttachedAsync();

        Answer exported = await browser.SendAsync("GET", Rights("/export?format=human"));

        Assert.Equal(StatusCodes.Status200OK, exported.Status);
        Assert.Equal(
            "record",
            exported.Json().GetProperty("sections").EnumerateArray().First().GetProperty("name").GetString());
        Assert.Equal([_record], _deployment.ExportLedger.Taken.Select(taken => taken.Subject));

        _deployment.Clock.Advance(Settings.SessionStepUpRecency.Default + TimeSpan.FromMinutes(1));

        Answer stale = await browser.SendAsync("GET", Rights("/export?format=human"));

        Assert.Equal(
            (StatusCodes.Status403Forbidden, ErrorCodes.StepUpRequired.ToString()),
            (stale.Status, stale.Text("code")));
    }

    /// <summary>
    /// REC-ATT-002, chapter 09 section 7a: a record attached to no account, and one
    /// attached to another, are answered as one no record bears.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ATT_002_ARecordNotAttachedToTheAccountIsAnsweredAsNoneAsync()
    {
        SubjectId unattached = Held();
        SubjectId elsewhere = Held();

        Browser browser = await AttachedAsync();

        (_deployment.Records.Of(elsewhere).AttachedTo, _deployment.Records.Of(elsewhere).AttachedAt) =
            (SubjectId.New(Randomness), _deployment.Clock.GetUtcNow());

        Answer missed = await browser.SendAsync("GET", "/account/records/" + unattached);
        Answer another = await browser.SendAsync("PUT", "/account/records/" + elsewhere, ("name", "Someone Else"));

        Assert.Equal(
            [
                (StatusCodes.Status404NotFound, ErrorCodes.RecordNotFound.ToString()),
                (StatusCodes.Status404NotFound, ErrorCodes.RecordNotFound.ToString()),
            ],
            new[] { missed, another }.Select(answer => (answer.Status, answer.Text("code"))));
    }

    /// <summary>
    /// REC-ATT-002: while an attached record is deleting with its account, it is read and
    /// changed in nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_ATT_002_ADeletingRecordIsReadAndNotChangedAsync()
    {
        Browser browser = await AttachedAsync();

        (_deployment.Records.Of(_record).State, _deployment.Records.Of(_record).DeletingBy) =
            (PersonRecordState.Deleting, DeletionOrigin.Self);

        Answer read = await browser.SendAsync("GET", Rights());
        Answer changed = await browser.SendAsync("PUT", Rights(), ("name", "Someone Else"));

        Assert.Equal(StatusCodes.Status200OK, read.Status);
        Assert.Equal(
            (StatusCodes.Status409Conflict, ErrorCodes.RecordStateConflict.ToString()),
            (changed.Status, changed.Text("code")));
    }

    private string Rights(string path = "") => "/account/records/" + _record + path;

    // A record collected in the shop a day ago, holding a verified email.
    private SubjectId Held()
    {
        var subject = SubjectId.New(Randomness);
        DateTimeOffset verified = _deployment.Clock.GetUtcNow() - TimeSpan.FromDays(1);

        _deployment.Records.Hold(
            new HeldRecord(subject, _shop, verified)
            {
                Language = Language,
                Name = DisplayName.TryParse("Mona Saleh", out DisplayName name) ? name : null,
                Contacts =
                {
                    [IdentifierKind.Email] = new CollectedContact(IdentifierKind.Email, "guest@example.test", verified),
                },
            });
        _deployment.AccountStates.Hold(subject, AccountState.Active);

        return subject;
    }

    // A browser signed in on an account the record is attached to.
    private async Task<Browser> AttachedAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        (_deployment.Records.Of(_record).AttachedTo, _deployment.Records.Of(_record).AttachedAt) =
            (_deployment.Directory.Created[^1].Subject, _deployment.Clock.GetUtcNow());

        return browser;
    }
}
