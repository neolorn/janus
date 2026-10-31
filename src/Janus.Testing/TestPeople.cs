using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Testing;

/// <summary>
/// The people a test signs in over a running host, each through the library's own
/// endpoints: a customer registered with the links the in-memory transports hold, the
/// first administrator enrolled with a software passkey through the enrolment link, a
/// staff member invited by an administrator with the roles the test names and enrolled
/// through the invitation, and a visitor, who holds no account.
/// </summary>
/// <remarks>
/// Implements LIB-TEST-003. Each person is a browser of their own, which makes first
/// contact before anything else, so every state-changing request carries the
/// synchronizer token the browser profile bound to it; nothing is skipped, bypassed or
/// short-cut. A link is pressed in the browser that asked for it, as the landing page
/// presses it (API-LAND-001). The legal documents a registration accepts are the versions
/// the deployment publishes at the time, read as the frontend reads them, so a deployment
/// that has published none refuses the customer as it would refuse anyone.
/// </remarks>
public sealed class TestPeople
{
    // A date of birth that is an adult's on every host, for the age step (REG-PROF-002).
    private const string DateOfBirth = "1990-01-01";

    private const string Label = "Software passkey";

    private readonly Uri _address;

    private readonly SignOnClient _client;

    private readonly DocumentName _terms;

    private readonly Func<HttpMessageHandler> _channel;

    private readonly InMemoryNotifications _notifications;

    private readonly RandomNumberGenerator _randomness;

    /// <summary>
    /// Makes the people of one running host.
    /// </summary>
    /// <param name="address">
    /// Where the test serves the host's application: its origin and the path the browser
    /// profile is mounted under.
    /// </param>
    /// <param name="client">The host's browser application, which a registration names.</param>
    /// <param name="terms">The legal document the host publishes as its terms of service.</param>
    /// <param name="channel">
    /// Makes what carries a browser's requests to the application, one for each person;
    /// it keeps no cookies of its own.
    /// </param>
    /// <param name="notifications">The notification handler the host registered.</param>
    /// <param name="randomness">Where each software passkey's credential is drawn from.</param>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public TestPeople(
        Uri address,
        SignOnClient client,
        DocumentName terms,
        Func<HttpMessageHandler> channel,
        InMemoryNotifications notifications,
        RandomNumberGenerator randomness)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(notifications);
        ArgumentNullException.ThrowIfNull(randomness);

        _address = TestSession.Mounted(address);
        _client = client;
        _terms = terms;
        _channel = channel;
        _notifications = notifications;
        _randomness = randomness;
    }

    /// <summary>
    /// Registers a customer through the registration endpoints, the address and the
    /// number verified by the links their messages carried, with a password and no second
    /// step, accepting the published terms and notice; the terms step signs the customer
    /// in.
    /// </summary>
    /// <param name="email">The customer's email.</param>
    /// <param name="phone">The customer's number, or nothing to pass the phone step over.</param>
    /// <param name="password">The password, which the step-up of the session presents.</param>
    /// <param name="cancellationToken">Abandons the registration.</param>
    /// <returns>The customer's session.</returns>
    /// <exception cref="ArgumentException">The password is empty.</exception>
    /// <exception cref="InvalidOperationException">An endpoint answered what no step of the registration answers.</exception>
    public async ValueTask<TestSession> CustomerAsync(
        EmailAddress email,
        PhoneNumber? phone,
        string password,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        Browser? browser = Browser();

        try
        {
            using (HttpClient registering = Client(browser))
            {
                await BegunAsync(registering, null, cancellationToken).ConfigureAwait(false);
                await SendAsync(registering, HttpMethod.Put, "register/email", new JsonObject { ["value"] = email.Value }, HttpStatusCode.Accepted, cancellationToken).ConfigureAwait(false);
                await VerifiedAsync(registering, "email", Mailed(email), cancellationToken).ConfigureAwait(false);
                await PhoneAsync(registering, phone, cancellationToken).ConfigureAwait(false);
                await SendAsync(registering, HttpMethod.Post, "register/confirm", null, HttpStatusCode.OK, cancellationToken).ConfigureAwait(false);
                await SendAsync(
                        registering,
                        HttpMethod.Put,
                        "register/security",
                        new JsonObject { ["password"] = password, ["secondStep"] = "none" },
                        HttpStatusCode.OK,
                        cancellationToken)
                    .ConfigureAwait(false);
                await AcceptedAsync(registering, cancellationToken).ConfigureAwait(false);
            }

            TestSession customer = await SignedInAsync(browser, email.Value, passkey: null, password, cancellationToken)
                .ConfigureAwait(false);

            browser = null;

            return customer;
        }
        finally
        {
            browser?.Dispose();
        }
    }

    /// <summary>
    /// Enrols the deployment's first administrator with a software passkey through the
    /// enrolment link <c>bootstrap</c> printed, then signs the administrator in with it.
    /// </summary>
    /// <param name="enrolment">The enrolment link.</param>
    /// <param name="email">The administrator's email, as <c>bootstrap</c> was given it.</param>
    /// <param name="cancellationToken">Abandons the enrolment.</param>
    /// <returns>The administrator's session.</returns>
    /// <exception cref="ArgumentNullException">The link is absent.</exception>
    /// <exception cref="ArgumentException">The link is not an enrolment link.</exception>
    /// <exception cref="InvalidOperationException">An endpoint answered what no step of the enrolment answers.</exception>
    public async ValueTask<TestSession> AdministratorAsync(
        Uri enrolment,
        EmailAddress email,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(enrolment);

        string token = Links.Token(enrolment, "enrolment");
        SoftwarePasskey? passkey = new(_address, _randomness);

        try
        {
            using (Browser enrolling = Browser())
            {
                using HttpClient client = Client(enrolling);

                await FirstContactAsync(client, cancellationToken).ConfigureAwait(false);
                await SendAsync(client, HttpMethod.Post, "enrol/begin", new JsonObject { ["token"] = token }, HttpStatusCode.OK, cancellationToken).ConfigureAwait(false);
                await EnrolledAsync(client, passkey, cancellationToken).ConfigureAwait(false);
            }

            TestSession administrator = await SignInAsync(email.Value, passkey, cancellationToken).ConfigureAwait(false);

            passkey = null;

            return administrator;
        }
        finally
        {
            passkey?.Dispose();
        }
    }

    /// <summary>
    /// Has an administrator invite a staff member into an organization with the roles the
    /// test names, then registers the staff member through the invitation with a software
    /// passkey, acknowledges the membership and signs the staff member in with the
    /// passkey.
    /// </summary>
    /// <param name="administrator">The session of an administrator of the organization.</param>
    /// <param name="organization">The organization the staff member joins.</param>
    /// <param name="email">The email the invitation binds and is sent to.</param>
    /// <param name="phone">The staff member's number, or nothing to pass the phone step over.</param>
    /// <param name="roles">The roles the membership grants across the organization.</param>
    /// <param name="cancellationToken">Abandons the invitation and the enrolment.</param>
    /// <returns>The staff member's session.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    /// <exception cref="InvalidOperationException">An endpoint answered what no step of the enrolment answers.</exception>
    public async ValueTask<TestSession> StaffMemberAsync(
        TestSession administrator,
        OrganizationId organization,
        EmailAddress email,
        PhoneNumber? phone,
        IReadOnlyList<RoleName> roles,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(administrator);
        ArgumentNullException.ThrowIfNull(roles);

        await SteppedAsync(
                administrator,
                HttpMethod.Post,
                "admin/organizations/" + organization + "/invitations",
                new JsonObject
                {
                    ["email"] = email.Value,
                    ["roles"] = new JsonArray([.. roles.Select(role => JsonValue.Create(role.ToString()))]),
                },
                HttpStatusCode.Created,
                cancellationToken)
            .ConfigureAwait(false);

        string invitation = Links.Token(Mailed(email), "invitation");
        SoftwarePasskey? passkey = new(_address, _randomness);

        try
        {
            using (Browser registering = Browser())
            {
                using HttpClient client = Client(registering);

                await BegunAsync(client, invitation, cancellationToken).ConfigureAwait(false);
                await PhoneAsync(client, phone, cancellationToken).ConfigureAwait(false);
                await SendAsync(client, HttpMethod.Post, "register/confirm", null, HttpStatusCode.OK, cancellationToken).ConfigureAwait(false);
                await EnrolledAsync(client, passkey, cancellationToken).ConfigureAwait(false);
                await AcceptedAsync(client, cancellationToken).ConfigureAwait(false);

                Answer standing = await SendAsync(client, HttpMethod.Get, "account/invitation", null, HttpStatusCode.OK, cancellationToken).ConfigureAwait(false);

                await SendAsync(
                        client,
                        HttpMethod.Post,
                        "account/invitation/acknowledge",
                        new JsonObject { ["invitationId"] = standing.Text("id") },
                        HttpStatusCode.NoContent,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            TestSession staff = await SignInAsync(email.Value, passkey, cancellationToken).ConfigureAwait(false);

            passkey = null;

            return staff;
        }
        finally
        {
            passkey?.Dispose();
        }
    }

    /// <summary>
    /// A visitor: a browser of its own, signed in as nobody, carrying what a session
    /// carries save a sign-in session, for what a guest does with no account (D-222).
    /// </summary>
    /// <returns>The visitor, which has made no request yet.</returns>
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The visitor is an HttpClient made with disposeHandler: true, so the browser is disposed with it.")]
    public TestVisitor Visitor() => new(Browser(contact: new Uri(_address, "auth/session")), _address);

    // A state-changing request an administrator makes, stepped up once where the gate
    // of its action asks for it, as the frontend does (AUTH-STEP-002).
    private static async Task SteppedAsync(
        TestSession session,
        HttpMethod method,
        string route,
        JsonObject body,
        HttpStatusCode expected,
        CancellationToken cancellationToken)
    {
        Answer answer = await Exchange.AnswerAsync(session, method, route, body, cancellationToken).ConfigureAwait(false);

        if (answer.Status is HttpStatusCode.Forbidden && answer.Code is "auth.stepup.required")
        {
            await session.StepUpAsync(cancellationToken).ConfigureAwait(false);

            answer = await Exchange.AnswerAsync(session, method, route, body, cancellationToken).ConfigureAwait(false);
        }

        if (answer.Status != expected)
        {
            throw Exchange.Unexpected(method, route, answer);
        }
    }

    private static Task<Answer> SendAsync(
        HttpClient client,
        HttpMethod method,
        string route,
        JsonObject? body,
        HttpStatusCode expected,
        CancellationToken cancellationToken) =>
        Exchange.SendAsync(client, method, route, body, [expected], cancellationToken);

    // BFF-CSRF-005a: the first request a browser makes is answered with the
    // pre-authentication cookie and the token bound to it.
    private static Task<Answer> FirstContactAsync(HttpClient client, CancellationToken cancellationToken) =>
        Exchange.SendAsync(client, HttpMethod.Get, "auth/session", null, [HttpStatusCode.Unauthorized], cancellationToken);

    // REG-SESS-001, REG-INV-001: a registration session for the host's client, opened by
    // the invitation's press where there is one, with the age step answered.
    private async Task BegunAsync(HttpClient client, string? invitation, CancellationToken cancellationToken)
    {
        var begun = new JsonObject { ["clientId"] = _client.ClientId };

        if (invitation is not null)
        {
            begun["invitationToken"] = invitation;
        }

        await FirstContactAsync(client, cancellationToken).ConfigureAwait(false);
        await SendAsync(client, HttpMethod.Post, "register", begun, HttpStatusCode.Created, cancellationToken).ConfigureAwait(false);
        await SendAsync(client, HttpMethod.Put, "register/age", new JsonObject { ["dateOfBirth"] = DateOfBirth }, HttpStatusCode.OK, cancellationToken).ConfigureAwait(false);
    }

    // REG-SESS-002 AC3: the phone step, staged and verified, or passed over.
    private async Task PhoneAsync(HttpClient client, PhoneNumber? phone, CancellationToken cancellationToken)
    {
        if (phone is not { } number)
        {
            await SendAsync(client, HttpMethod.Post, "register/phone/skip", null, HttpStatusCode.OK, cancellationToken).ConfigureAwait(false);

            return;
        }

        await SendAsync(client, HttpMethod.Put, "register/phone", new JsonObject { ["value"] = number.Value }, HttpStatusCode.Accepted, cancellationToken).ConfigureAwait(false);
        await VerifiedAsync(client, "phone", Texted(number), cancellationToken).ConfigureAwait(false);
    }

    // REG-SESS-003: the identifier of a kind still waiting is verified by a press of the
    // link its message carried, in the browser that staged it.
    private static async Task VerifiedAsync(
        HttpClient client,
        string kind,
        IEnumerable<string> sent,
        CancellationToken cancellationToken)
    {
        Answer state = await SendAsync(client, HttpMethod.Get, "register", null, HttpStatusCode.OK, cancellationToken).ConfigureAwait(false);

        JsonElement waiting = state.Body.GetProperty("identifiers").EnumerateArray().FirstOrDefault(staged =>
            staged.GetProperty("kind").GetString() == kind && !staged.GetProperty("verified").GetBoolean());

        if (waiting.ValueKind is not JsonValueKind.Object)
        {
            throw new InvalidOperationException("GET register holds no " + kind + " waiting for its verification.");
        }

        await SendAsync(
                client,
                HttpMethod.Post,
                "register/verify/" + waiting.GetProperty("id").GetString(),
                new JsonObject { ["linkToken"] = Links.Token(sent, "registration"), ["press"] = true },
                HttpStatusCode.NoContent,
                cancellationToken)
            .ConfigureAwait(false);
    }

    // AUTH-FACT-014, REG-SESS-006: a passkey enrolled through the library's ceremony, in
    // a registration session or an enrolment session alike.
    private static async Task EnrolledAsync(HttpClient client, SoftwarePasskey passkey, CancellationToken cancellationToken)
    {
        Answer options = await SendAsync(
                client,
                HttpMethod.Post,
                "auth/webauthn/register/begin",
                new JsonObject { ["kind"] = "passkey", ["label"] = Label },
                HttpStatusCode.OK,
                cancellationToken)
            .ConfigureAwait(false);

        await SendAsync(
                client,
                HttpMethod.Post,
                "auth/webauthn/register/complete",
                new JsonObject { ["credential"] = passkey.Create(options.Body), ["label"] = Label },
                HttpStatusCode.OK,
                cancellationToken)
            .ConfigureAwait(false);
    }

    // REG-SESS-007: the terms step, accepting the versions the deployment publishes now.
    private async Task AcceptedAsync(HttpClient client, CancellationToken cancellationToken)
    {
        Answer notice = await SendAsync(client, HttpMethod.Get, "privacy/notice", null, HttpStatusCode.OK, cancellationToken).ConfigureAwait(false);
        Answer terms = await SendAsync(client, HttpMethod.Get, "privacy/documents/" + _terms, null, HttpStatusCode.OK, cancellationToken).ConfigureAwait(false);

        await SendAsync(
                client,
                HttpMethod.Post,
                "register/terms",
                new JsonObject { ["termsVersion"] = terms.Text("version"), ["noticeVersion"] = notice.Text("version") },
                HttpStatusCode.Created,
                cancellationToken)
            .ConfigureAwait(false);
    }

    // AUTH-FACT-014: an ordinary sign-in with the passkey, in a browser of its own.
    private async Task<TestSession> SignInAsync(string identifier, SoftwarePasskey passkey, CancellationToken cancellationToken)
    {
        Browser? browser = Browser();

        try
        {
            using (HttpClient client = Client(browser))
            {
                await FirstContactAsync(client, cancellationToken).ConfigureAwait(false);

                Answer begun = await SendAsync(client, HttpMethod.Post, "auth/begin", new JsonObject { ["identifier"] = identifier }, HttpStatusCode.OK, cancellationToken).ConfigureAwait(false);
                Answer signedIn = await SendAsync(
                        client,
                        HttpMethod.Post,
                        "auth/factor",
                        new JsonObject
                        {
                            ["challengeId"] = begun.Text("challengeId"),
                            ["factor"] = "passkey",
                            ["value"] = passkey.Get(TestSession.Assertion(begun.Body)),
                        },
                        HttpStatusCode.OK,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (signedIn.Text("status") is not "complete")
                {
                    throw new InvalidOperationException("POST auth/factor answered " + signedIn.Text("status") + " to a passkey.");
                }
            }

            TestSession session = await SignedInAsync(browser, identifier, passkey, password: null, cancellationToken)
                .ConfigureAwait(false);

            browser = null;

            return session;
        }
        finally
        {
            browser?.Dispose();
        }
    }

    // AUTH-SESS-001: the session the browser now holds, read for its subject.
    private async Task<TestSession> SignedInAsync(
        Browser browser,
        string identifier,
        SoftwarePasskey? passkey,
        string? password,
        CancellationToken cancellationToken)
    {
        using HttpClient client = Client(browser);

        Answer session = await SendAsync(client, HttpMethod.Get, "auth/session", null, HttpStatusCode.OK, cancellationToken).ConfigureAwait(false);

        return new TestSession(
            browser,
            _address,
            SubjectId.Parse(session.Text("subject"), provider: null),
            identifier,
            passkey,
            password);
    }

    private Browser Browser(Uri? contact = null) => new(_address, _channel(), contact);

    private HttpClient Client(Browser browser) => new(browser, disposeHandler: false) { BaseAddress = _address };

    // LIB-TEST-003: a link is read as the value its message carries.
    private IEnumerable<string> Mailed(EmailAddress email) => Linked(_notifications.SentTo(email));

    private IEnumerable<string> Texted(PhoneNumber number) => Linked(_notifications.SentTo(number));

    private static IEnumerable<string> Linked(IEnumerable<SendRequest> sent) =>
        sent.Where(request => request.Values.ContainsKey("link")).Select(request => request.Values["link"]);
}
