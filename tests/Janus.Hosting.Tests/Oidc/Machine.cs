using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Janus.Hosting.Tests.Oidc;

/// <summary>
/// A caller on the machine profile: it holds no cookie, sets no header a frontend
/// sets, and writes a form the way a relying party's own library writes one, or a body
/// the way a provider's callback writes one.
/// </summary>
/// <param name="deployment">What it talks to.</param>
internal sealed class Machine(Deployment deployment)
{
    /// <summary>
    /// Posts a form to one of the library's machine routes.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="fields">What the form carries.</param>
    /// <returns>What came back.</returns>
    public Task<Answer> PostAsync(string path, params (string Name, string? Value)[] fields) =>
        SendAsync(path, Form(fields), cookie: null, bearer: null);

    /// <summary>
    /// Posts a form while carrying a browser's session cookie, which is what a
    /// machine route refuses (BFF-MACH-001 AC2).
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="cookie">The cookie header to carry.</param>
    /// <param name="fields">What the form carries.</param>
    /// <returns>What came back.</returns>
    public Task<Answer> PostCarryingAsync(
        string path,
        string cookie,
        params (string Name, string? Value)[] fields) =>
        SendAsync(path, Form(fields), cookie, bearer: null);

    /// <summary>
    /// Reads one of the library's machine routes with an access token.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="bearer">The access token.</param>
    /// <returns>What came back.</returns>
    public Task<Answer> GetAsync(string path, string bearer) =>
        SendAsync(path, body: null, cookie: null, bearer);

    /// <summary>
    /// Reads one of the library's machine routes as a provider's callback does: with
    /// its parameters in the query string and nothing else.
    /// </summary>
    /// <param name="path">The path, with its query string.</param>
    /// <returns>What came back.</returns>
    public Task<Answer> CallAsync(string path) =>
        SendAsync(path, body: null, cookie: null, bearer: null);

    /// <summary>
    /// Reads one of the library's machine routes while carrying a browser's session
    /// cookie, which is what a machine route refuses (BFF-MACH-001 AC2).
    /// </summary>
    /// <param name="path">The path, with its query string.</param>
    /// <param name="cookie">The cookie header to carry.</param>
    /// <returns>What came back.</returns>
    public Task<Answer> CallCarryingAsync(string path, string cookie) =>
        SendAsync(path, body: null, cookie, bearer: null);

    /// <summary>
    /// Posts a body to one of the library's machine routes as a provider's callback
    /// does: exactly the bytes it writes, of the type it names, and nothing else.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="contentType">What the body is.</param>
    /// <param name="body">The body.</param>
    /// <param name="cookie">A cookie header to carry, which a machine route refuses.</param>
    /// <returns>What came back.</returns>
    public Task<Answer> DeliverAsync(string path, string contentType, string body, string? cookie = null) =>
        SendAsync(path, (contentType, body), cookie, bearer: null);

    private static (string ContentType, string Text) Form(IReadOnlyList<(string Name, string? Value)> fields) =>
        (
            "application/x-www-form-urlencoded",
            string.Join(
                "&",
                fields
                    .Where(field => field.Value is not null)
                    .Select(field => Uri.EscapeDataString(field.Name)
                        + "="
                        + Uri.EscapeDataString(field.Value!))));

    private static PathString Path(string path, out QueryString query)
    {
        int at = path.IndexOf('?', StringComparison.Ordinal);

        if (at < 0)
        {
            query = QueryString.Empty;

            return new PathString(path);
        }

        query = new QueryString(path[at..]);

        return new PathString(path[..at]);
    }

    private async Task<Answer> SendAsync(
        string path,
        (string ContentType, string Text)? body,
        string? cookie,
        string? bearer)
    {
        var context = new DefaultHttpContext();

        context.Request.Method = body is null ? "GET" : "POST";
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("identity.example.test");
        context.Request.Path = Path(path, out QueryString query);
        context.Request.QueryString = query;

        if (cookie is not null)
        {
            context.Request.Headers.Cookie = cookie;
        }

        if (bearer is not null)
        {
            context.Request.Headers.Authorization = "Bearer " + bearer;
        }

        if (body is (string contentType, string text))
        {
            context.Request.ContentType = contentType;
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(text));
            context.Request.ContentLength = context.Request.Body.Length;
            context.Features.Set<IHttpRequestBodyDetectionFeature>(new RequestBodyPresent());
        }

        var answered = new ResponseBody();

        context.Response.Body = answered;

        await deployment.SendAsync(context);

        return new Answer(
            context.Response.StatusCode,
            answered.Taken(),
            context.Response.Headers.Location.ToString() is { Length: > 0 } where ? where : null,
            [.. context.Response.Headers.SetCookie.Select(header => header!)]);
    }
}
