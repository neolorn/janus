using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Conformance.Tests;
using Janus.Core;
using Xunit;

namespace Janus.Testing.Tests;

/// <summary>
/// The people of a running host and their sessions, signed in through the library's own
/// endpoints of a host built on the public surface (LIB-TEST-003).
/// </summary>
/// <param name="host">The conformance suite's sample host, whose first administrator was enrolled through the link bootstrap printed.</param>
[Trait("kind", "integration")]
public sealed class PeopleTests(SampleHost host) : IClassFixture<SampleHost>
{
    private const string Password = "quietly-folded-heron-lantern-48";

    private static readonly RoleName Auditor = RoleName.Parse("auditor");

    /// <summary>
    /// LIB-TEST-003 AC1: a customer, the first administrator and a staff member are each
    /// signed in as their own subject, and each calls the host's endpoints with what the
    /// session grants: the auditor reads the audit, the customer does not.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_003_AC1_ACustomerAnAdministratorAndAStaffMemberAreSignedInAndCallTheHostsEndpointsAsync()
    {
        await PublishedAsync();
        using TestSession customer = await host.People.CustomerAsync(
            Email("customer.one@sample.example.test"),
            Phone("+201000000021"),
            Password,
            TestContext.Current.CancellationToken);
        using TestSession staff = await host.People.StaffMemberAsync(
            host.Administrator,
            host.AdministrativeOrganization,
            Email("staff.one@sample.example.test"),
            Phone("+201000000022"),
            [Auditor],
            TestContext.Current.CancellationToken);

        JsonElement customerSession = await ReadAsync(customer, "auth/session");
        JsonElement administratorSession = await ReadAsync(host.Administrator, "auth/session");
        JsonElement staffSession = await ReadAsync(staff, "auth/session");
        using HttpResponseMessage audited = await staff.GetAsync(
            new Uri("admin/audit?subject=" + staff.Subject, UriKind.Relative),
            TestContext.Current.CancellationToken);
        using HttpResponseMessage refused = await customer.GetAsync(
            new Uri("admin/audit?subject=" + customer.Subject, UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(customer.Subject.ToString(), customerSession.GetProperty("subject").GetString());
        Assert.Equal(host.Administrator.Subject.ToString(), administratorSession.GetProperty("subject").GetString());
        Assert.Equal(staff.Subject.ToString(), staffSession.GetProperty("subject").GetString());
        Assert.Equal(HttpStatusCode.OK, audited.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }

    /// <summary>
    /// LIB-TEST-003 AC3: a staff member enrolled with a software passkey in the
    /// administrative organization and signed in with it holds a session at
    /// <c>aal2</c>, phishing-resistant, and meets the export's gate, which that
    /// organization's policy sets at <c>aal2</c> with phishing resistance (chapter 10
    /// section 5a), with no step-up.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_003_AC3_AStaffMemberSignedInWithASoftwarePasskeyMeetsAPhishingResistantGateAtAal2Async()
    {
        await PublishedAsync();
        using TestSession staff = await host.People.StaffMemberAsync(
            host.Administrator,
            host.AdministrativeOrganization,
            Email("staff.two@sample.example.test"),
            Phone("+201000000023"),
            [Auditor],
            TestContext.Current.CancellationToken);

        JsonElement session = await ReadAsync(staff, "auth/session");
        using HttpResponseMessage exported = await staff.GetAsync(
            new Uri("privacy/export?format=machine", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal("aal2", session.GetProperty("assuranceLevel").GetString());
        Assert.True(session.GetProperty("phishingResistant").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, exported.StatusCode);
    }

    /// <summary>
    /// LIB-TEST-003 AC5: a state-changing request made through a session passes the
    /// browser profile's CSRF stages, where the same request from a client that carries
    /// neither the header nor the token is refused at them.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_003_AC5_AStateChangingRequestThroughATestSessionPassesTheCsrfStagesAsync()
    {
        using var bare = new HttpClient(host.Channel()) { BaseAddress = new Uri(SampleHost.Address.AbsoluteUri + "/") };

        using HttpResponseMessage published = await host.Administrator.PostAsJsonAsync(
            new Uri("admin/notices", UriKind.Relative),
            Version("A notice."),
            TestContext.Current.CancellationToken);
        using HttpResponseMessage refused = await bare.PostAsJsonAsync(
            new Uri("admin/notices", UriKind.Relative),
            Version("A notice."),
            TestContext.Current.CancellationToken);
        JsonElement refusal = await refused.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal("auth.session.csrfinvalid", refusal.GetProperty("code").GetString());
    }

    /// <summary>
    /// LIB-TEST-003: a session steps up with the person's own factor, the administrator's
    /// passkey and the customer's password, through the library's step-up.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_003_ASessionStepsUpWithThePersonsPasskeyOrPasswordAsync()
    {
        await PublishedAsync();
        using TestSession customer = await host.People.CustomerAsync(
            Email("customer.two@sample.example.test"),
            Phone("+201000000024"),
            Password,
            TestContext.Current.CancellationToken);

        Exception? administrator = await Record.ExceptionAsync(
            () => host.Administrator.StepUpAsync(TestContext.Current.CancellationToken).AsTask());
        Exception? stepped = await Record.ExceptionAsync(
            () => customer.StepUpAsync(TestContext.Current.CancellationToken).AsTask());

        Assert.Null(administrator);
        Assert.Null(stepped);
    }

    private static EmailAddress Email(string entered) =>
        EmailAddress.TryParse(entered, out EmailAddress address) ? address : throw new ArgumentException(entered);

    private static PhoneNumber Phone(string entered) =>
        PhoneNumber.TryParse(entered, out PhoneNumber number) ? number : throw new ArgumentException(entered);

    private static object Version(string text) => new { text, material = false };

    private static async Task<JsonElement> ReadAsync(HttpClient session, string route)
    {
        using HttpResponseMessage answered = await session.GetAsync(
            new Uri(route, UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, answered.StatusCode);

        return await answered.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    // The notice and the terms a registration accepts, published by the administrator.
    private async Task PublishedAsync()
    {
        using HttpResponseMessage notice = await host.Administrator.PostAsJsonAsync(
            new Uri("admin/notices", UriKind.Relative),
            Version("The privacy notice."),
            TestContext.Current.CancellationToken);
        using HttpResponseMessage terms = await host.Administrator.PostAsJsonAsync(
            new Uri("admin/documents/" + SampleHost.Terms + "/versions", UriKind.Relative),
            Version("The terms of service."),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, notice.StatusCode);
        Assert.Equal(HttpStatusCode.OK, terms.StatusCode);
    }
}
