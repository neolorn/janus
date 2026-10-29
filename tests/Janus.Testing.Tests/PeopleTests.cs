using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
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
public sealed class PeopleTests(SampleHost host) : IClassFixture<SampleHost>, IDisposable
{
    private const string Password = "quietly-folded-heron-lantern-48";

    private static readonly RoleName Auditor = RoleName.Parse("auditor");

    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    /// <summary>
    /// LIB-TEST-003, REG-PROF-002: a customer answers the age step with the date of birth
    /// 1990-01-01, an adult's on every host.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_003_ACustomerAnswersTheAgeStepWithTheDateOfBirth19900101Async()
    {
        await PublishedAsync();
        var requests = new RequestsInMemory();
        using TestSession customer = await People(requests).CustomerAsync(
            Email("customer.three@sample.example.test"),
            Phone("+201000000025"),
            Password,
            TestContext.Current.CancellationToken);

        RequestMade age = Assert.Single(
            requests.Made,
            made => made.Method == HttpMethod.Put && made.Path == SampleHost.Prefix + "/register/age");
        JsonElement answered = JsonSerializer.Deserialize<JsonElement>(age.Body);

        Assert.Equal("1990-01-01", answered.GetProperty("dateOfBirth").GetString());
    }

    /// <summary>
    /// LIB-TEST-003, REG-SESS-003: a customer's address and number are each verified by a
    /// press of the registration link the in-memory transport holds for it, and no code is
    /// typed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_003_EachVerificationLinkTheTransportsHoldIsPressedAsync()
    {
        await PublishedAsync();
        var requests = new RequestsInMemory();
        EmailAddress email = Email("customer.four@sample.example.test");
        PhoneNumber phone = Phone("+201000000026");
        using TestSession customer = await People(requests).CustomerAsync(
            email,
            phone,
            Password,
            TestContext.Current.CancellationToken);

        JsonElement[] verifications =
        [
            .. requests.Made
                .Where(made => made.Method == HttpMethod.Post
                    && made.Path.StartsWith(SampleHost.Prefix + "/register/verify/", StringComparison.Ordinal))
                .Select(made => JsonSerializer.Deserialize<JsonElement>(made.Body)),
        ];

        Assert.Equal(2, verifications.Length);
        Assert.All(verifications, verification =>
        {
            Assert.True(verification.GetProperty("press").GetBoolean());
            Assert.False(verification.TryGetProperty("code", out _));
        });
        Assert.Contains(
            host.Mail.SentTo(email),
            mail => mail.Body.Contains("#registration." + Token(verifications[0]), StringComparison.Ordinal));
        Assert.Contains(
            host.Sms.SentTo(phone),
            message => message.Text.Contains("#registration." + Token(verifications[1]), StringComparison.Ordinal));
    }

    /// <summary>
    /// LIB-TEST-003: the first administrator's passkey and a staff member's are each
    /// labelled <c>Software passkey</c>, as the person's credentials list them.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_003_EachPasskeyIsLabelledSoftwarePasskeyAsync()
    {
        await PublishedAsync();
        using TestSession staff = await host.People.StaffMemberAsync(
            host.Administrator,
            host.AdministrativeOrganization,
            Email("staff.three@sample.example.test"),
            Phone("+201000000027"),
            [Auditor],
            TestContext.Current.CancellationToken);

        JsonElement administrator = await ReadAsync(host.Administrator, "account/credentials");
        JsonElement member = await ReadAsync(staff, "account/credentials");

        Assert.Equal(["Software passkey"], Passkeys(administrator));
        Assert.Equal(["Software passkey"], Passkeys(member));
    }

    /// <summary>
    /// LIB-TEST-003: a customer, the first administrator and a staff member are each
    /// signed in as their own subject, and each session reaches the library's routes
    /// with what it grants: the auditor reads the audit, the customer does not.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_003_EachPersonIsSignedInAsTheirOwnSubjectAndReachesTheLibrarysRoutesItGrantsAsync()
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
    /// LIB-TEST-003 AC5 (D-222): a state-changing request a guest makes through a visitor
    /// passes the browser profile's CSRF stages, where the same request from a client that
    /// carries neither the header nor the token is refused at them, and the visitor
    /// carries no sign-in session.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_003_AC5_AStateChangingRequestThroughATestVisitorPassesTheCsrfStagesAsync()
    {
        using TestVisitor visitor = host.People.Visitor();
        using var bare = new HttpClient(host.Channel()) { BaseAddress = new Uri(SampleHost.Address.AbsoluteUri + "/") };
        var asked = new { kind = "email", value = "guest.one@sample.example.test" };

        using HttpResponseMessage accepted = await visitor.PostAsJsonAsync(
            new Uri("records/access", UriKind.Relative),
            asked,
            TestContext.Current.CancellationToken);
        using HttpResponseMessage refused = await bare.PostAsJsonAsync(
            new Uri("records/access", UriKind.Relative),
            asked,
            TestContext.Current.CancellationToken);
        using HttpResponseMessage session = await visitor.GetAsync(
            new Uri("auth/session", UriKind.Relative),
            TestContext.Current.CancellationToken);
        JsonElement refusal = await refused.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal("auth.session.csrfinvalid", refusal.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, session.StatusCode);
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

    /// <inheritdoc/>
    public void Dispose() => _randomness.Dispose();

    private static string? Token(JsonElement verification) => verification.GetProperty("linkToken").GetString();

    // The label of each passkey the person's credentials list.
    private static string?[] Passkeys(JsonElement credentials) =>
    [
        .. credentials.EnumerateArray()
            .Where(credential => credential.GetProperty("kind").GetString() is "passkey")
            .Select(credential => credential.GetProperty("label").GetString()),
    ];

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

    // The people of the host, whose browsers' requests are kept as they are made.
    private TestPeople People(RequestsInMemory requests) =>
        new(
            SampleHost.Address,
            SampleHost.Client,
            SampleHost.Terms,
            () => requests.Over(host.Channel()),
            host.Mail,
            host.Sms,
            _randomness);

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
