using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Privacy;

/// <summary>
/// The records of processing of chapter 09 sections 8 and 8a: the generated register
/// in the regulator's template shape, and the three fields a person supplies to it.
/// </summary>
[Trait("kind", "unit")]
public sealed class ProcessingRecordsEndpointTests : IAsyncDisposable
{
    private static readonly OrganizationId Company =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private static readonly string[] Assessments = ["wiki/lia-2026", "wiki/dpia-2026"];

    private readonly Deployment _deployment = new();

    /// <summary>
    /// A deployment able to register a browser, hosted where it says it is.
    /// </summary>
    public ProcessingRecordsEndpointTests()
    {
        Flow.Prepare(_deployment);
        _deployment.Configuration.Set(Settings.HostingEnvironment, "a managed Kubernetes cluster");
        _deployment.Configuration.Set(Settings.HostingLocation, HostingLocation.Inside);
        _deployment.Configuration.Set(Settings.HostingCrossBorderBasis, "the regulator's permit");
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// PRIV-ROPA-001 AC1: the generated register carries the template's fields, in
    /// the template's order, at the register and at every row.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_ROPA_001_AC1_TheGeneratedOutputMatchesTheTemplatesFieldSetAndOrderingAsync()
    {
        Browser browser = await AuthorisedAsync();

        Answer generated = await browser.SendAsync("GET", "/admin/ropa?format=template");

        Assert.Equal(StatusCodes.Status200OK, generated.Status);

        JsonElement register = generated.Json();

        Assert.Equal(
            [
                "generatedAt",
                "hostingEnvironment",
                "hostingLocation",
                "crossBorderBasis",
                "dataOwner",
                "organizationalSecurityMeasures",
                "assessmentLinks",
                "records",
                "recipients",
                "flags",
            ],
            Named(register));

        Assert.Equal(
            [
                "purpose",
                "dataCategories",
                "subjectCategories",
                "lawfulBasis",
                "nonSensitive",
                "sensitive",
                "children",
                "sensitiveCategories",
                "retention",
                "recipients",
                "disposalMeasures",
                "rolesWithAccess",
                "technicalSecurityMeasures",
                "assessment",
            ],
            Named(register.GetProperty("records").EnumerateArray().First()));

        Assert.Equal(
            [
                "name",
                "characterisation",
                "dataReceived",
                "location",
                "agreementReference",
                "crossBorderBasis",
                "callback",
            ],
            Named(register.GetProperty("recipients").EnumerateArray().First()));

        Assert.Equal("a managed Kubernetes cluster", register.GetProperty("hostingEnvironment").GetString());
        Assert.Equal("inside", register.GetProperty("hostingLocation").GetString());
    }

    /// <summary>
    /// PRIV-ROPA-002 and LIB-EXT-001, D-166 (270): a deployment that registered a mail
    /// server and its own transports, and set no address for a shipped mail transport,
    /// reports the mail server row the library makes true.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_ROPA_002_ARegisteredMailServerIsInTheRegisterAsync()
    {
        Browser browser = await AuthorisedAsync();

        JsonElement register = (await browser.SendAsync("GET", "/admin/ropa?format=template")).Json();

        Assert.Contains(
            "mail server",
            register.GetProperty("recipients").EnumerateArray().Select(recipient => recipient.GetProperty("name").GetString()));
    }

    /// <summary>
    /// PRIV-ROPA-001 AC2, and chapter 09 section 8a: the three supplied fields are
    /// flagged while nobody has stated them, and stand in the register once someone
    /// has.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_ROPA_001_AC2_TheThreeSuppliedFieldsAreStatedOverTheEndpointAsync()
    {
        Browser browser = await AuthorisedAsync();

        JsonElement before = (await browser.SendAsync("GET", "/admin/ropa?format=template")).Json();

        Assert.Equal(
            ["data-owner-missing", "organizational-measures-missing", "assessment-links-missing"],
            Findings(before).Where(Supplied));

        Answer stated = await browser.SendAsync(
            "PUT",
            "/admin/compliance/assessments",
            ("dataOwner", "the head of customer operations"),
            ("organizationalSecurityMeasures", "annual training and a clear-desk rule"),
            ("assessmentLinks", Assessments));

        Assert.Equal(StatusCodes.Status204NoContent, stated.Status);

        JsonElement after = (await browser.SendAsync("GET", "/admin/ropa?format=template")).Json();

        Assert.Equal("the head of customer operations", after.GetProperty("dataOwner").GetString());
        Assert.Equal(
            "annual training and a clear-desk rule",
            after.GetProperty("organizationalSecurityMeasures").GetString());
        Assert.Equal(
            ["wiki/lia-2026", "wiki/dpia-2026"],
            after.GetProperty("assessmentLinks").EnumerateArray().Select(link => link.GetString()));

        Assert.DoesNotContain(Findings(after), Supplied);
    }

    /// <summary>
    /// API-CONV-002 AC3, CONV-CODE-006 AC3 (D-183): the data owner and the organizational
    /// security measures are free text, so one given blank or longer than 1024 characters
    /// after trimming is refused naming it before the service is reached, and the
    /// statement held is left as it was.
    /// </summary>
    /// <param name="member">The member written outside the bound.</param>
    /// <param name="character">What it is written of.</param>
    /// <param name="length">How many of it.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("dataOwner", ' ', 2)]
    [InlineData("dataOwner", 'd', 1025)]
    [InlineData("organizationalSecurityMeasures", ' ', 1)]
    [InlineData("organizationalSecurityMeasures", 'd', 1025)]
    public async Task API_CONV_002_AStatementOutsideTheBoundIsRefusedBeforeTheServiceAsync(
        string member,
        char character,
        int length)
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        string written = new(character, length);

        Answer refused = await browser.SendAsync(
            "PUT",
            "/admin/compliance/assessments",
            ("dataOwner", member == "dataOwner" ? written : "the head of customer operations"),
            (
                "organizationalSecurityMeasures",
                member == "dataOwner" ? "annual training" : written));

        Assert.Equal(StatusCodes.Status400BadRequest, refused.Status);
        Assert.Equal(ErrorCodes.RequestMalformed.ToString(), refused.Text("code"));
        Assert.Equal(member, refused.Json().GetProperty("details").GetProperty("member").GetString());
        Assert.Null(_deployment.Compliance.Held.DataOwner);
    }

    /// <summary>
    /// API-CONV-002, 09 section 8a (D-183): a statement within the bound is kept
    /// trimmed, and a member the statement omits is cleared.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task API_CONV_002_AStatementIsKeptTrimmedAndAnOmittedOneIsClearedAsync()
    {
        Browser browser = await AuthorisedAsync();

        Answer stated = await browser.SendAsync(
            "PUT",
            "/admin/compliance/assessments",
            ("dataOwner", "  the head of customer operations "),
            ("organizationalSecurityMeasures", " annual training "));

        Assert.Equal(StatusCodes.Status204NoContent, stated.Status);
        Assert.Equal("the head of customer operations", _deployment.Compliance.Held.DataOwner);
        Assert.Equal("annual training", _deployment.Compliance.Held.OrganizationalSecurityMeasures);

        Answer narrowed = await browser.SendAsync(
            "PUT",
            "/admin/compliance/assessments",
            ("dataOwner", "the head of customer operations"));

        Assert.Equal(StatusCodes.Status204NoContent, narrowed.Status);
        Assert.Null(_deployment.Compliance.Held.OrganizationalSecurityMeasures);
    }

    /// <summary>
    /// Chapter 09 section 8: the endpoint generates the template shape, so a request
    /// naming no shape, or another one, is malformed rather than answered with a
    /// guess.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_ROPA_001_AShapeTheEndpointDoesNotGenerateIsMalformedAsync()
    {
        Browser browser = await AuthorisedAsync();

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            (await browser.SendAsync("GET", "/admin/ropa")).Status);

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            (await browser.SendAsync("GET", "/admin/ropa?format=spreadsheet")).Status);
    }

    /// <summary>
    /// 10 section 2.1: generating the register takes <c>ropa:read</c> and stating the
    /// supplied fields takes <c>compliance:manage</c>, so a signed-in account holding
    /// neither is refused both.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_ROPA_001_AnAccountHoldingNeitherPermissionIsRefusedAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Assert.Equal(
            StatusCodes.Status403Forbidden,
            (await browser.SendAsync("GET", "/admin/ropa?format=template")).Status);

        Assert.Equal(
            StatusCodes.Status403Forbidden,
            (await browser.SendAsync(
                "PUT",
                "/admin/compliance/assessments",
                ("dataOwner", "whoever asked"))).Status);
    }

    private static bool Supplied(string finding) =>
        finding is "data-owner-missing"
            or "organizational-measures-missing"
            or "assessment-links-missing";

    private static IEnumerable<string> Named(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name);

    private static IEnumerable<string> Findings(JsonElement register) =>
        register.GetProperty("flags").EnumerateArray()
            .Select(flag => flag.GetProperty("finding").GetString()!);

    /// <summary>
    /// AUTHZ-GATE-006 AC3: a restriction of the caller committed after the gate step and
    /// before the first write refuses the statements, which stay as they stood.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GATE_006_AC3_ARestrictionCommittedSinceTheGateStepRefusesTheStatementsAsync()
    {
        Browser browser = await AuthorisedAsync();
        string? owner = _deployment.Compliance.Held.DataOwner;

        await RestrictedSinceTheGateStep.RefusesAsync(
            _deployment,
            () => browser.SendAsync(
                "PUT",
                "/admin/compliance/assessments",
                ("dataOwner", "the head of customer operations"),
                ("organizationalSecurityMeasures", "annual training")));

        Assert.Equal(owner, _deployment.Compliance.Held.DataOwner);
    }

    private async Task<Browser> AuthorisedAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        SubjectId subject = _deployment.Directory.Created[^1].Subject;

        _deployment.Administers(Company);
        _deployment.Gate.Grant(subject, Company, Permissions.RecordsOfProcessingRead);
        _deployment.Gate.Grant(subject, Company, Permissions.ComplianceManage);

        return browser;
    }
}
