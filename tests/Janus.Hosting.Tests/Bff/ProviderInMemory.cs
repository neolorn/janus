using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Janus.Hosting.Tests.Bff;

/// <summary>
/// The provider as this application's own server reaches it: a connection of its own,
/// carrying no cookie, landing on the same deployment's machine profile.
/// </summary>
/// <param name="deployment">What the back channel reaches.</param>
internal sealed class ProviderInMemory(Deployment deployment) : HttpMessageHandler
{
    private readonly List<Uri> _asked = [];

    /// <summary>
    /// Every address the back channel asked, in order.
    /// </summary>
    public IReadOnlyList<Uri> Asked => _asked;

    /// <summary>
    /// Whether the token endpoint is reachable at all.
    /// </summary>
    public bool Reachable { get; set; } = true;

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Uri address = request.RequestUri
            ?? throw new InvalidOperationException("The back channel named no address.");

        _asked.Add(address);

        if (!Reachable)
        {
            throw new HttpRequestException("The provider is unreachable.");
        }

        var context = new DefaultHttpContext();

        context.Request.Method = request.Method.Method;
        context.Request.Scheme = address.Scheme;
        context.Request.Host = new HostString(address.Authority);
        context.Request.Path = new PathString(address.AbsolutePath);
        context.Request.QueryString = new QueryString(address.Query);

        if (request.Content is HttpContent sent)
        {
            context.Request.ContentType = sent.Headers.ContentType?.ToString();
            context.Request.Body = new MemoryStream(
                await sent.ReadAsByteArrayAsync(cancellationToken));
            context.Request.ContentLength = context.Request.Body.Length;
            context.Features.Set<IHttpRequestBodyDetectionFeature>(new RequestBodyPresent());
        }

        var body = new ResponseBody();

        context.Response.Body = body;

        await deployment.SendAsync(context);

        return new HttpResponseMessage((HttpStatusCode)context.Response.StatusCode)
        {
            Content = new StringContent(body.Taken()),
        };
    }
}
