using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// The host's own endpoints, mapped after the browser profile beside the library's and
/// written with the public surface alone, as a host writes them (BFF-HOST-001 to
/// BFF-HOST-004).
/// </summary>
internal sealed class HostEndpoints
{
    /// <summary>
    /// The catalogue, which answers anyone.
    /// </summary>
    public const string Catalogue = "/catalogue";

    /// <summary>
    /// The views of the catalogue a visitor records, a change that answers anyone.
    /// </summary>
    public const string Views = "/catalogue/views";

    /// <summary>
    /// The workspaces, which answer only a signed-in person.
    /// </summary>
    public const string Workspaces = "/workspaces";

    private readonly List<string> _reached = [];

    /// <summary>
    /// The endpoints that ran, by method and path, in the order they ran.
    /// </summary>
    public IReadOnlyList<string> Reached => _reached;

    /// <summary>
    /// Maps them.
    /// </summary>
    /// <param name="endpoints">Where the host maps its endpoints.</param>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public void Map(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        _ = endpoints.MapGet(Catalogue, context => Ran(context, StatusCodes.Status200OK)).AllowNoSession();
        _ = endpoints.MapPost(Views, context => Ran(context, StatusCodes.Status204NoContent)).AllowNoSession();
        _ = endpoints.MapGet(Workspaces, context => Ran(context, StatusCodes.Status200OK));
    }

    private Task Ran(HttpContext context, int status)
    {
        _reached.Add(context.Request.Method + " " + context.Request.Path);
        context.Response.StatusCode = status;

        return Task.CompletedTask;
    }
}
