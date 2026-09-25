using System;
using System.Buffers.Text;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Janus.Authentication;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Bff;

/// <summary>
/// What the browser is given for a session: two opaque values under attributes no
/// deployment can weaken (BFF-SESS-001 to BFF-SESS-005, BFF-CSRF-005, BFF-CSRF-006).
/// </summary>
[Trait("kind", "unit")]
public sealed class BrowserCookieTests : IDisposable
{
    private const string SessionCookie = "__Host-identity-session";
    private const string CsrfCookie = "__Host-identity-csrf";

    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    /// <summary>
    /// BFF-SESS-002 AC1: every issue carries the four attributes, the prefix being
    /// carried by the name itself, which is what forbids a domain.
    /// </summary>
    [Fact]
    public void BFF_SESS_002_AC1_EveryIssueCarriesTheFourAttributes()
    {
        string written = Written(ApplicationKind.Public, SessionCookie);

        Assert.StartsWith("__Host-", written, StringComparison.Ordinal);
        Assert.Contains("secure", written, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", written, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=", written, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", written, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", written, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// BFF-SESS-001 AC1: the browser receives the two opaque values and nothing else,
    /// so no token, claim, permission or role name crosses in a cookie.
    /// </summary>
    [Fact]
    public void BFF_SESS_001_AC1_NothingButTheTwoOpaqueValuesReachesTheBrowser()
    {
        IssuedSession issued = Issued();
        DefaultHttpContext context = Answering(issued, ApplicationKind.Public);

        string[] written = [.. context.Response.Headers.SetCookie.Select(header => header!)];

        Assert.Equal(2, written.Length);
        Assert.Equal(
            [SessionCookie + "=" + issued.Secret.Value, CsrfCookie + "=" + issued.CsrfToken.Value],
            [.. written.Select(header => header.Split("; ", StringSplitOptions.None)[0])]);
        Assert.DoesNotContain(issued.Id.ToString(), string.Join(" ", written), StringComparison.Ordinal);
    }

    /// <summary>
    /// AUTHZ-CACHE-002 AC1: what the browser holds is two drawn values, so no
    /// permission and no role name can be in a cookie: every permission the library
    /// declares and every administrative role of chapter 10 section 3 is absent from
    /// what is written, and the values decode to bytes that spell none of them.
    /// </summary>
    [Fact]
    public void AUTHZ_CACHE_002_AC1_NoPermissionOrRoleNameIsInACookie()
    {
        IssuedSession issued = Issued();
        string written = string.Join(
            " ",
            Answering(issued, ApplicationKind.Public).Response.Headers.SetCookie.Select(header => header!));
        string decoded = string.Join(
            " ",
            new[] { issued.Secret, issued.CsrfToken }.Select(carried =>
                Encoding.UTF8.GetString(Base64Url.DecodeFromChars(carried.Value))));

        Assert.All(
            Named(),
            name =>
            {
                Assert.DoesNotContain(name, written, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(name, decoded, StringComparison.OrdinalIgnoreCase);
            });
    }

    /// <summary>
    /// BFF-SESS-001 AC2: the value decodes to drawn bytes and says nothing, in
    /// particular not which session it is.
    /// </summary>
    [Fact]
    public void BFF_SESS_001_AC2_TheCookieValueYieldsNothingWhenDecoded()
    {
        IssuedSession issued = Issued();

        foreach (OpaqueToken carried in new[] { issued.Secret, issued.CsrfToken })
        {
            byte[] decoded = Base64Url.DecodeFromChars(carried.Value);

            Assert.Equal(32, decoded.Length);
            Assert.Equal(-1, decoded.AsSpan().IndexOf(Encoding.UTF8.GetBytes(issued.Id.ToString())));
        }
    }

    /// <summary>
    /// BFF-SESS-003 AC1: nothing scopes a cookie to a parent domain, so one
    /// application is not carrying another one's session.
    /// </summary>
    [Fact]
    public void BFF_SESS_003_AC1_NoCookieIsScopedToAParentDomain() => Assert.All(
        [.. Both(ApplicationKind.Public), .. Both(ApplicationKind.Management)],
        written => Assert.DoesNotContain("domain=", written, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// BFF-SESS-005 AC1: signing out leaves the browser carrying neither value.
    /// </summary>
    [Fact]
    public void BFF_SESS_005_AC1_SigningOutClearsBothCookies()
    {
        var context = new DefaultHttpContext();

        new BrowserSessionCookies(ApplicationKind.Public).Clear(context.Response);

        string[] written = [.. context.Response.Headers.SetCookie.Select(header => header!)];

        Assert.Equal(2, written.Length);
        Assert.All(written, header => Assert.Contains("expires=", header, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(written, header => header.StartsWith(SessionCookie + "=;", StringComparison.Ordinal));
        Assert.Contains(written, header => header.StartsWith(CsrfCookie + "=;", StringComparison.Ordinal));
    }

    /// <summary>
    /// BFF-CSRF-005 AC1: the management application has no external entry point, so
    /// its cookies are withheld from every cross-site request.
    /// </summary>
    [Fact]
    public void BFF_CSRF_005_AC1_TheManagementApplicationsCookieIsStrict() => Assert.All(
        Both(ApplicationKind.Management),
        written => Assert.Contains("samesite=strict", written, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// BFF-CSRF-005 AC2: an application reached by a sign-in or verification link
    /// carries its session on that first top-level navigation, which strict would
    /// withhold.
    /// </summary>
    [Fact]
    public void BFF_CSRF_005_AC2_APublicApplicationCarriesItsSessionOnAnInboundLink() => Assert.All(
        Both(ApplicationKind.Public),
        written => Assert.Contains("samesite=lax", written, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// BFF-CSRF-005 AC3: no cookie the library sets is issued cross-site-capable.
    /// </summary>
    [Fact]
    public void BFF_CSRF_005_AC3_NoCookieIsIssuedWithSameSiteNone() => Assert.All(
        [.. Both(ApplicationKind.Public), .. Both(ApplicationKind.Management)],
        written => Assert.DoesNotContain("samesite=none", written, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// BFF-CSRF-006 AC1: the token is set beside the session cookie and is the one
    /// value the first-party frontend reads, so obtaining it costs no round trip.
    /// </summary>
    [Fact]
    public void BFF_CSRF_006_AC1_TheTokenIsSetBesideTheSessionAndReadableByScript()
    {
        Assert.DoesNotContain(
            "httponly",
            Written(ApplicationKind.Public, CsrfCookie),
            StringComparison.OrdinalIgnoreCase);

        Assert.Contains(
            "httponly",
            Written(ApplicationKind.Public, SessionCookie),
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// AUTH-SESS-003 AC1: the session token is in a cookie the browser will not hand
    /// to a script, and the library writes to no browser store at all. The one value
    /// a script does read is the synchronizer token, which is not a session token and
    /// is what BFF-CSRF-006 requires it to read.
    /// </summary>
    [Fact]
    public void AUTH_SESS_003_AC1_NoSessionTokenIsReadableByAScript()
    {
        Assert.Contains(
            "httponly",
            Written(ApplicationKind.Public, SessionCookie),
            StringComparison.OrdinalIgnoreCase);

        Assert.Empty(Repository
            .Sources()
            .Where(file => File.ReadLines(file).Any(line =>
                line.Contains("localStorage", StringComparison.Ordinal)
                || line.Contains("sessionStorage", StringComparison.Ordinal)))
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// AUTH-SESS-003 AC2: the cookie carries the four attributes on every issue.
    /// </summary>
    [Fact]
    public void AUTH_SESS_003_AC2_TheCookieCarriesAllFourAttributes()
    {
        string written = Written(ApplicationKind.Management, SessionCookie);

        Assert.StartsWith("__Host-", written, StringComparison.Ordinal);
        Assert.Contains("secure", written, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", written, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", written, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// BFF-SESS-002 AC2: no configuration key weakens an attribute, there being no
    /// key read anywhere the attributes are decided. The one file of the boundary that
    /// reads a key is the flood limit of stage 4, which writes no cookie.
    /// </summary>
    [Fact]
    public void BFF_SESS_002_AC2_NoConfigurationKeyWeakensAnAttribute()
    {
        Assert.Equal(
            ["SourceRateLimiting.cs"],
            Repository
                .Sources()
                .Where(file => File.ReadLines(file).Any(line =>
                    line.Contains("IConfigurationStore", StringComparison.Ordinal)
                    || line.Contains("Settings.", StringComparison.Ordinal)))
                .Select(Path.GetFileName)
                .Order(StringComparer.Ordinal));

        Assert.DoesNotContain("Cookie", Repository.Source("SourceRateLimiting"), StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public void Dispose() => _randomness.Dispose();

    // Every permission the library declares and every role chapter 10 section 3 names.
    private static string[] Named() =>
    [
        .. Permissions.All.Select(permission => permission.ToString()),
        "system-administrator",
        "auditor",
        "support",
    ];

    private IssuedSession Issued() => new(
        SessionId.New(TimeProvider.System),
        OpaqueToken.Draw(_randomness),
        OpaqueToken.Draw(_randomness));

    private static DefaultHttpContext Answering(IssuedSession issued, ApplicationKind application)
    {
        var context = new DefaultHttpContext();

        new BrowserSessionCookies(application).Write(context.Response, issued);

        return context;
    }

    private string[] Both(ApplicationKind application) =>
        [.. Answering(Issued(), application).Response.Headers.SetCookie.Select(header => header!)];

    private string Written(ApplicationKind application, string name) => Assert.Single(
        Both(application),
        header => header.StartsWith(name + "=", StringComparison.Ordinal));
}
