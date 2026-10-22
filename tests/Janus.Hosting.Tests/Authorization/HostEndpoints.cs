using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
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
    /// Who is asking, read by an endpoint that answers anyone.
    /// </summary>
    public const string Visitor = "/catalogue/visitor";

    /// <summary>
    /// The workspaces, which answer only a signed-in person.
    /// </summary>
    public const string Workspaces = "/workspaces";

    /// <summary>
    /// The person's sessions, as the host lists them.
    /// </summary>
    public const string Sessions = "/workspaces/sessions";

    /// <summary>
    /// The person's preferences, as the host sets them.
    /// </summary>
    public const string Preferences = "/workspaces/preferences";

    /// <summary>
    /// An endpoint whose failure carries a code the library does not list.
    /// </summary>
    public const string Unlisted = "/workspaces/unlisted";

    // A code of the form every code takes that the library does not list.
    private static readonly ErrorCode Unknown = ErrorCode.Parse("workspace.archive.sealed");

    // The host writes its own answers with options of its own, the library's being the
    // library's.
    private static readonly JsonSerializerOptions Written = new(JsonSerializerDefaults.Web);

    private readonly List<string> _reached = [];

    private readonly List<RequestAccess> _read = [];

    /// <summary>
    /// The endpoints that ran, by method and path, in the order they ran.
    /// </summary>
    public IReadOnlyList<string> Reached => _reached;

    /// <summary>
    /// The access each endpoint that read one was given, in the order they read it.
    /// </summary>
    public IReadOnlyList<RequestAccess> Read => _read;

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
        _ = endpoints.MapGet(Visitor, Visiting).AllowNoSession();
        _ = endpoints.MapGet(Workspaces, context => Ran(context, StatusCodes.Status200OK));
        _ = endpoints.MapGet(Sessions, ListAsync);
        _ = endpoints.MapDelete(Sessions + "/{id}", EndAsync);
        _ = endpoints.MapPut(Preferences, PreferAsync);
        _ = endpoints.MapGet(Unlisted, (bool typed) => typed
            ? Answers.Of(Result.Failure<int>(Error.From(Unknown)), value => Results.Ok(value))
            : Answers.Refused(Error.From(Unknown)));
    }

    private Task Ran(HttpContext context, int status)
    {
        _reached.Add(context.Request.Method + " " + context.Request.Path);
        context.Response.StatusCode = status;

        return Task.CompletedTask;
    }

    private IResult Visiting(IRequestAccess access) =>
        Answers.Of(access.Current(), Seen(held => Results.NoContent()));

    private Task<IResult> ListAsync(
        IRequestAccess access,
        ISessions sessions,
        CancellationToken cancellationToken) =>
        access.Current().Match(
            async held =>
            {
                _read.Add(held);

                return Answers.Of(
                    await sessions.ListAsync(held.Context, held.Session, cancellationToken),
                    listed => Results.Json(listed.Select(session => new { id = session.Id.ToString(), current = session.Current }), Written));
            },
            Refused);

    private Task<IResult> EndAsync(
        SessionId id,
        IRequestAccess access,
        ISessions sessions,
        CancellationToken cancellationToken) =>
        access.Current().Match(
            async held => Answers.Of(await sessions.EndAsync(held.Context, id, cancellationToken), Results.NoContent()),
            Refused);

    private Task<IResult> PreferAsync(
        IRequestAccess access,
        IAccount accounts,
        CancellationToken cancellationToken) =>
        access.Current().Match(
            async held =>
            {
                _read.Add(held);

                return Answers.Of(
                    await accounts.SetPreferencesAsync(
                        held.Context,
                        "en",
                        timeZone: null,
                        new Dictionary<string, string>(StringComparer.Ordinal),
                        cancellationToken),
                    Results.NoContent());
            },
            Refused);

    private static Task<IResult> Refused(Error error) => Task.FromResult(Answers.Refused(error));

    private Func<RequestAccess, IResult> Seen(Func<RequestAccess, IResult> answered) => held =>
    {
        _read.Add(held);

        return answered(held);
    };
}
