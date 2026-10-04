using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Xunit;
using Xunit.Sdk;

namespace Janus.Hosting.Tests.Bff;

/// <summary>
/// The check the test host runs on every response it carries: an endpoint answers a
/// code it declares, one its mounting answers or one of the pipeline's, and no other
/// (CONV-DESIGN-006).
/// </summary>
[Trait("kind", "unit")]
public sealed class EndpointAnswersTests : IAsyncDisposable
{
    private readonly Deployment _deployment = new();

    /// <summary>
    /// A deployment a browser can register against, so a request reaches an endpoint.
    /// </summary>
    public EndpointAnswersTests() => Flow.Prepare(_deployment);

    /// <summary>
    /// CONV-DESIGN-006 AC4: a response carrying a code its endpoint does not declare
    /// fails the test that received it.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_006_AC4_AnUndeclaredCodeFailsTheTest()
    {
        DefaultHttpContext context = Answered(
            Mounted("/declared", EndpointDeclaration.Answering(ErrorCodes.CodeInvalid)),
            ErrorCodes.CodeExpired);

        FailException failure = Assert.Throws<FailException>(() => EndpointAnswers.Hold(context));

        Assert.Contains(ErrorCodes.CodeExpired.ToString(), failure.Message, StringComparison.Ordinal);
        Assert.Contains("/declared", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// CONV-DESIGN-006 AC4: the code an endpoint declares passes, and so does one of the
    /// pipeline's own, which chapter 09 lists once and no endpoint declares.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_006_AC4_ADeclaredCodeAndThePipelinesPass()
    {
        RouteEndpoint endpoint = Mounted("/declared", EndpointDeclaration.Answering(ErrorCodes.CodeInvalid));

        EndpointAnswers.Hold(Answered(endpoint, ErrorCodes.CodeInvalid));
        EndpointAnswers.Hold(Answered(endpoint, ErrorCodes.Throttled));
        EndpointAnswers.Hold(Answered(endpoint, ErrorCodes.SystemFault));
    }

    /// <summary>
    /// CONV-DESIGN-006 AC4: what an endpoint's mounting answers passes where the
    /// mounting gives it and fails where it does not. An endpoint that requires no
    /// session, reads no body and binds no value is answered neither the absent session
    /// nor the malformed request; one on the browser profile is answered the forgery
    /// layers' refusal.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_006_AC4_AMountingAnswerPassesOnlyWhereTheMountingGivesIt()
    {
        RouteEndpoint bare = Mounted("/bare", EndpointDeclaration.Answering());
        RouteEndpoint binding = Mounted(
            "/binding/{subject}",
            EndpointDeclaration.Answering().Binding<SubjectId>("subject"));

        EndpointAnswers.Hold(Answered(bare, ErrorCodes.SessionCsrfInvalid));
        EndpointAnswers.Hold(Answered(binding, ErrorCodes.RequestMalformed));
        _ = Assert.Throws<FailException>(() => EndpointAnswers.Hold(Answered(bare, ErrorCodes.RequestMalformed)));
        _ = Assert.Throws<FailException>(() => EndpointAnswers.Hold(Answered(bare, ErrorCodes.SessionExpired)));
    }

    /// <summary>
    /// CONV-DESIGN-006 AC4: a refusal whose body the host cannot read is a failure and
    /// not a pass, so no test escapes the check by where it has the response written.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_006_AC4_ARefusalTheHostCannotReadFailsTheTest()
    {
        var context = new DefaultHttpContext();

        context.SetEndpoint(Mounted("/declared", EndpointDeclaration.Answering()));
        context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        context.Response.ContentType = "application/json";

        _ = Assert.Throws<FailException>(() => EndpointAnswers.Hold(context));
    }

    /// <summary>
    /// CONV-DESIGN-006 AC4: the check runs on what the host itself carries, and what it
    /// derives for a mounted endpoint is what that endpoint's mounting answers: the
    /// account's own read requires a session, and a browser holding none is answered
    /// the absent session without a failure of the check.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_006_AC4_TheHostHoldsEveryResponseItCarriesAsync()
    {
        var browser = new Browser(_deployment);

        _ = await browser.SendAsync("GET", "/register");

        Answer refused = await browser.SendAsync("GET", "/account/");

        Assert.Equal(StatusCodes.Status401Unauthorized, refused.Status);
        Assert.Contains(
            ErrorCodes.SessionExpired,
            EndpointAnswers.Mounting(
                Assert.Single(
                    _deployment.Endpoints.OfType<RouteEndpoint>(),
                    endpoint => endpoint.RoutePattern.RawText == "/account/"
                        && endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods[0] == "GET")));
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _deployment.DisposeAsync();

    private static RouteEndpoint Mounted(string pattern, EndpointDeclaration declaration) =>
        new(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse(pattern),
            order: 0,
            new EndpointMetadataCollection(declaration, new HttpMethodMetadata(["POST"])),
            pattern);

    private static DefaultHttpContext Answered(RouteEndpoint endpoint, ErrorCode code)
    {
        var context = new DefaultHttpContext();
        var written = new ResponseBody();

        context.SetEndpoint(endpoint);
        context.Response.StatusCode = ApiStatus.Of(code);
        context.Response.ContentType = "application/json";
        context.Response.Body = written;
        written.Write(Encoding.UTF8.GetBytes("{\"code\":\"" + code + "\",\"correlationId\":\"c\",\"details\":{}}"));

        return context;
    }
}
