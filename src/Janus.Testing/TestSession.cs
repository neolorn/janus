using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Testing;

/// <summary>
/// A signed-in person's browser at a host's application: a client that carries the
/// session cookie, the synchronizer token and <c>X-Identity-Request</c> on every request
/// to the application's origin, with the person's subject and a step-up with the
/// person's passkey or password.
/// </summary>
/// <remarks>
/// Implements LIB-TEST-003, BFF-CSRF-001 and BFF-CSRF-003. Its base address is where the
/// browser profile is mounted, so a route of the library is given relative to it and a
/// route of the host's own from the root. A request it makes passes the browser
/// profile's CSRF stages as one of the first-party frontend's does, and nothing more: the
/// session it carries is judged by every other stage as any session is.
/// </remarks>
public sealed class TestSession : HttpClient
{
    private readonly string _identifier;

    private readonly SoftwarePasskey? _passkey;

    private readonly string? _password;

    // The session is made by TestPeople, from the browser a person signed in in, and is
    // stepped up with the one factor that person holds.
    internal TestSession(
        Browser browser,
        Uri address,
        SubjectId subject,
        string identifier,
        SoftwarePasskey? passkey,
        string? password)
        : base(browser, disposeHandler: true)
    {
        BaseAddress = Mounted(address);
        Subject = subject;
        _identifier = identifier;
        _passkey = passkey;
        _password = password;
    }

    /// <summary>
    /// The person's subject.
    /// </summary>
    public SubjectId Subject { get; }

    /// <summary>
    /// Steps the session up with the person's passkey or password, through
    /// <c>POST /auth/begin</c> and <c>POST /auth/step-up</c>, as the frontend does when a
    /// request is refused <c>auth.stepup.required</c>; the refused request is then made
    /// again by the test.
    /// </summary>
    /// <param name="cancellationToken">Abandons the step-up.</param>
    /// <returns>The work of stepping up.</returns>
    /// <exception cref="InvalidOperationException">
    /// The step-up did not complete with the one factor the person holds.
    /// </exception>
    public async ValueTask StepUpAsync(CancellationToken cancellationToken)
    {
        Answer begun = await Exchange
            .SendAsync(
                this,
                HttpMethod.Post,
                "auth/begin",
                new JsonObject { ["identifier"] = _identifier },
                [HttpStatusCode.OK],
                cancellationToken)
            .ConfigureAwait(false);

        JsonNode presented = _passkey is not null
            ? _passkey.Get(Assertion(begun.Body))
            : JsonValue.Create(_password!);

        Answer stepped = await Exchange
            .SendAsync(
                this,
                HttpMethod.Post,
                "auth/step-up",
                new JsonObject
                {
                    ["challengeId"] = begun.Text("challengeId"),
                    ["factor"] = _passkey is not null ? "passkey" : "password",
                    ["value"] = presented,
                },
                [HttpStatusCode.OK],
                cancellationToken)
            .ConfigureAwait(false);

        if (stepped.Text("status") is not "complete")
        {
            throw new InvalidOperationException(
                "POST auth/step-up answered " + stepped.Text("status") + " to the one factor the person holds.");
        }
    }

    /// <summary>
    /// Where the browser profile is mounted, as a base address relative routes are read
    /// against.
    /// </summary>
    /// <param name="address">The address the application is served at.</param>
    /// <returns>The address, ending in a separator.</returns>
    internal static Uri Mounted(Uri address) =>
        address.AbsoluteUri.EndsWith('/') ? address : new Uri(address.AbsoluteUri + "/");

    /// <summary>
    /// The request options <c>POST /auth/begin</c> answers for an assertion.
    /// </summary>
    /// <param name="begun">The answer's body.</param>
    /// <returns>The options.</returns>
    /// <exception cref="InvalidOperationException">The answer carries none.</exception>
    internal static JsonElement Assertion(JsonElement begun)
    {
        // 09 POST /auth/begin spells the member webauthn and the endpoint's declaration
        // webAuthn; the member is read under either spelling.
        foreach (JsonProperty member in begun.EnumerateObject())
        {
            if (string.Equals(member.Name, "webauthn", StringComparison.OrdinalIgnoreCase))
            {
                return member.Value;
            }
        }

        throw new InvalidOperationException("POST auth/begin answered no WebAuthn challenge.");
    }
}
