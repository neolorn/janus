using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Janus.Hosting.Tests;

/// <summary>
/// One browser against a deployment: it keeps the cookies it is given and sends them
/// back, sets the header the frontend's interceptor sets, and reads what comes back.
/// </summary>
/// <param name="deployment">What it talks to.</param>
internal sealed class Browser(Deployment deployment)
{
    private const string Origin = "https://janus.example.test";

    private readonly Dictionary<string, string> _cookies = new(StringComparer.Ordinal);

    /// <summary>
    /// What the browser holds, by cookie name.
    /// </summary>
    public IReadOnlyDictionary<string, string> Cookies => _cookies;

    /// <summary>
    /// Forgets every cookie, which is what a different browser amounts to.
    /// </summary>
    public void Forget() => _cookies.Clear();

    /// <summary>
    /// Sends a request the way the frontend sends one.
    /// </summary>
    /// <param name="method">The method.</param>
    /// <param name="path">The path, with any query.</param>
    /// <param name="body">What to send as JSON, or nothing.</param>
    /// <param name="header">Whether to set the custom request header.</param>
    /// <param name="origin">What to claim as the origin, or nothing to claim none.</param>
    /// <param name="token">Whether to present the synchronizer token it holds.</param>
    /// <param name="contentType">What the body is sent as, where there is one.</param>
    /// <returns>What came back.</returns>
    public async Task<Answer> SendAsync(
        string method,
        string path,
        string? body = null,
        bool header = true,
        string? origin = Origin,
        bool token = true,
        string contentType = "application/json")
    {
        var context = new DefaultHttpContext();

        context.Request.Method = method;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("janus.example.test");
        context.Request.Path = Path(path, out QueryString query);
        context.Request.QueryString = query;
        context.Request.Headers["Sec-Fetch-Site"] = "same-origin";
        context.Request.Headers.AcceptLanguage = "en";

        if (header)
        {
            context.Request.Headers[BrowserCookies.RequestHeader] = "1";
        }

        if (origin is not null)
        {
            context.Request.Headers.Origin = origin;
        }

        if (_cookies.Count is not 0)
        {
            context.Request.Headers.Cookie = string.Join(
                "; ",
                _cookies.Select(held => held.Key + "=" + held.Value));
        }

        if (token && _cookies.TryGetValue(BrowserCookies.Csrf, out string? held))
        {
            context.Request.Headers[SynchronizerToken.Header] = held;
        }

        if (body is not null)
        {
            context.Request.ContentType = contentType;
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
            context.Request.ContentLength = context.Request.Body.Length;
            context.Features.Set<IHttpRequestBodyDetectionFeature>(new RequestBodyPresent());
        }

        var written = new ResponseBody();

        context.Response.Body = written;

        await deployment.SendAsync(context);

        return Taken(context, written);
    }

    /// <summary>
    /// Opens a stream and hands back what is being written to it, so a test can
    /// watch it while it runs.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="abort">What the browser goes away on.</param>
    /// <returns>The running request and the body it is writing.</returns>
    public (Task Running, ResponseBody Written) Open(string path, CancellationToken abort)
    {
        var context = new DefaultHttpContext();

        context.Request.Method = "GET";
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("janus.example.test");
        context.Request.Path = Path(path, out QueryString query);
        context.Request.QueryString = query;
        context.Request.Headers["Sec-Fetch-Site"] = "same-origin";
        context.Request.Headers.AcceptLanguage = "en";
        context.Request.Headers[BrowserCookies.RequestHeader] = "1";
        context.Request.Headers.Origin = Origin;
        context.RequestAborted = abort;

        if (_cookies.Count is not 0)
        {
            context.Request.Headers.Cookie = string.Join(
                "; ",
                _cookies.Select(held => held.Key + "=" + held.Value));
        }

        var written = new ResponseBody();

        context.Response.Body = written;

        return (deployment.SendAsync(context), written);
    }

    /// <summary>
    /// Sends a request with a JSON object built from the pairs given.
    /// </summary>
    /// <param name="method">The method.</param>
    /// <param name="path">The path.</param>
    /// <param name="fields">What the object holds.</param>
    /// <returns>What came back.</returns>
    public Task<Answer> SendAsync(
        string method,
        string path,
        params (string Name, object? Value)[] fields) =>
        SendAsync(method, path, Written(fields));

    private static string Written((string Name, object? Value)[] fields)
    {
        var written = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach ((string name, object? value) in fields)
        {
            written[name] = value;
        }

        return JsonSerializer.Serialize(written);
    }

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

    private Answer Taken(DefaultHttpContext context, ResponseBody written)
    {
        string[] cookies = [.. context.Response.Headers.SetCookie.Select(header => header!)];

        foreach (string header in cookies)
        {
            string pair = header.Split("; ", StringSplitOptions.None)[0];
            int at = pair.IndexOf('=', StringComparison.Ordinal);
            string name = pair[..at];
            string value = pair[(at + 1)..];

            if (value.Length is 0)
            {
                _ = _cookies.Remove(name);
            }
            else
            {
                _cookies[name] = value;
            }
        }

        return new Answer(
            context.Response.StatusCode,
            written.Taken(),
            context.Response.Headers.Location.ToString() is { Length: > 0 } where ? where : null,
            cookies)
        {
            Headers = context.Response.Headers.ToDictionary(
                written => written.Key,
                written => written.Value.ToString(),
                StringComparer.OrdinalIgnoreCase),
        };
    }
}
