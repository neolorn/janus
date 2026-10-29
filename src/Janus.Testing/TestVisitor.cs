using System;
using System.Net.Http;

namespace Janus.Testing;

/// <summary>
/// A guest's browser at a host's application, signed in as nobody: a client that
/// carries what a <see cref="TestSession"/> carries save a sign-in session (the
/// pre-authentication cookie, the synchronizer token, <c>X-Identity-Request</c>, the
/// origin and the fetch metadata) on every request to the application's origin, for
/// what a guest does with no account: a collection and the record access.
/// </summary>
/// <remarks>
/// Implements LIB-TEST-003 (D-222), BFF-CSRF-001, BFF-CSRF-003 and BFF-CSRF-005a. Its
/// base address is where the browser profile is mounted, as a session's is. Its first
/// request is preceded by the browser's first contact, <c>GET /auth/session</c>, which
/// the browser profile answers with the pre-authentication cookie and the token bound to
/// it, so a state-changing request passes the CSRF stages as the frontend's does; the
/// codes a collection or the record access sends are read from the in-memory
/// transports.
/// </remarks>
public sealed class TestVisitor : HttpClient
{
    // The visitor is made by TestPeople, from a browser of its own that holds nothing
    // yet and makes its first contact before its first request.
    internal TestVisitor(Browser browser, Uri address)
        : base(browser, disposeHandler: true) => BaseAddress = TestSession.Mounted(address);
}
