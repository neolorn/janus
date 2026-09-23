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
/// sets, and writes a form the way a relying party's own library writes one.
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
        SendAsync(path, fields, cookie: null, bearer: null);

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
        SendAsync(path, fields, cookie, bearer: null);

    /// <summary>
    /// Reads one of the library's machine routes with an access token.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="bearer">The access token.</param>
    /// <returns>What came back.</returns>
    public Task<Answer> GetAsync(string path, string bearer) =>
        SendAsync(path, fields: null, cookie: null, bearer);

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
        IReadOnlyList<(string Name, string? Value)>? fields,
        string? cookie,
        string? bearer)
    {
        var context = new DefaultHttpContext();

        context.Request.Method = fields is null ? "GET" : "POST";
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

        if (fields is not null)
        {
            string written = string.Join(
                "&",
                fields
                    .Where(field => field.Value is not null)
                    .Select(field => Uri.EscapeDataString(field.Name)
                        + "="
                        + Uri.EscapeDataString(field.Value!)));

            context.Request.ContentType = "application/x-www-form-urlencoded";
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(written));
            context.Request.ContentLength = context.Request.Body.Length;
            context.Features.Set<IHttpRequestBodyDetectionFeature>(new RequestBodyPresent());
        }

        var body = new ResponseBody();

        context.Response.Body = body;

        await deployment.SendAsync(context);

        return new Answer(
            context.Response.StatusCode,
            body.Taken(),
            context.Response.Headers.Location.ToString() is { Length: > 0 } where ? where : null,
            [.. context.Response.Headers.SetCookie.Select(header => header!)]);
    }
}
