using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Janus.Hosting.Tests.Bff;

/// <summary>
/// What status a failure answers with, and what an answer is allowed to carry
/// (API-CONV-002, API-CONV-003, BFF-ERR-002).
/// </summary>
[Trait("kind", "unit")]
public sealed class ApiStatusTests
{
    /// <summary>
    /// API-CONV-003 AC2: a failure that names no record answers 403; one that is
    /// about a particular record answers as its absence does, so that asking about a
    /// record one may not see and asking about one that is not there are the same
    /// question to the asker.
    /// </summary>
    [Fact]
    public void API_CONV_003_AC2_OnlyAFailureNamingNoRecordAnswersForbidden()
    {
        Assert.Equal(StatusCodes.Status403Forbidden, ApiStatus.Of(ErrorCodes.Denied));
        Assert.Equal(StatusCodes.Status403Forbidden, ApiStatus.Of(ErrorCodes.StepUpRequired));
        Assert.Equal(StatusCodes.Status403Forbidden, ApiStatus.Of(ErrorCodes.Restricted));

        Assert.Equal(StatusCodes.Status404NotFound, ApiStatus.Of(ErrorCodes.GrantNotFound));
        Assert.Equal(StatusCodes.Status404NotFound, ApiStatus.Of(ErrorCodes.CredentialNotFound));
        Assert.Equal(StatusCodes.Status404NotFound, ApiStatus.Of(ErrorCodes.ResourceNotFound));
    }

    /// <summary>
    /// REG-SESS-002 AC1, API-CONV-003: a step whose predecessor is incomplete is a
    /// failed state precondition, so it answers 409 and not as a body refused on what
    /// it says.
    /// </summary>
    [Fact]
    public void REG_SESS_002_AC1_AStepOutOfOrderIsAConflict() =>
        Assert.Equal(StatusCodes.Status409Conflict, ApiStatus.Of(ErrorCodes.RegistrationIncomplete));

    /// <summary>
    /// REG-IDENT-005 AC2, REG-IDENT-002: an unverified identifier made primary or named
    /// as the backup is a failed state precondition, and an identifier the account does
    /// not hold is a body refused on what it says.
    /// </summary>
    [Fact]
    public void REG_IDENT_005_AC2_AnUnverifiedIdentifierIsAConflict()
    {
        Assert.Equal(StatusCodes.Status409Conflict, ApiStatus.Of(ErrorCodes.IdentifierUnverified));
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, ApiStatus.Of(ErrorCodes.IdentifierInvalid));
    }

    /// <summary>
    /// IDN-LIFE-012, chapter 10 section 6: a social provider that could not be reached
    /// or read is a dependency outside the deployment, so its code maps to 502, which
    /// only a navigation's redirect carries.
    /// </summary>
    [Fact]
    public void IDN_LIFE_012_AProviderThatCannotBeReachedMapsToBadGateway() =>
        Assert.Equal(StatusCodes.Status502BadGateway, ApiStatus.Of(ErrorCodes.ProviderUnavailable));

    /// <summary>
    /// API-CONV-003: 401 is session death and nothing else, so no other failure may
    /// take it however much it looks like one.
    /// </summary>
    [Fact]
    public void Of_EveryCodeButSessionDeath_AnswersWithSomethingOtherThan401()
    {
        IEnumerable<ErrorCode> others = Catalogue().Where(code => code != ErrorCodes.SessionExpired);

        Assert.Equal(StatusCodes.Status401Unauthorized, ApiStatus.Of(ErrorCodes.SessionExpired));
        Assert.All(
            others,
            code => Assert.NotEqual(StatusCodes.Status401Unauthorized, ApiStatus.Of(code)));
    }

    /// <summary>
    /// REF-001 AC3, API-CONV-003: the table decides every code a request can be answered
    /// with, so no code can reach the boundary and be given a status by accident. A mail
    /// server's answer to the provisioning job is answered by no request and needs none
    /// (chapter 10 section 6).
    /// </summary>
    [Fact]
    public void REF_001_AC3_ACodeARequestCanBeAnsweredWithHasAStatus() =>
        Assert.All(
            Catalogue().Where(code => code != ErrorCodes.MailServerConflict),
            code => Assert.True(ApiStatus.Names(code), code.ToString()));

    /// <summary>
    /// BFF-ERR-002 AC1: a fault answers as a fault. The code behind it, and anything
    /// its context named, stays in the logs the correlation identifier resolves.
    /// </summary>
    [Fact]
    public async Task BFF_ERR_002_AC1_AFaultDisclosesOnlyTheCorrelationIdentifierAsync()
    {
        await using ServiceProvider logging = new ServiceCollection().AddLogging().BuildServiceProvider();

        var context = new DefaultHttpContext
        {
            RequestServices = logging,
            Response = { Body = new MemoryStream() },
            TraceIdentifier = "0HN7A2",
        };

        var fault = Error.From(
            ErrorCodes.PolicyUnregistered,
            "entity",
            JsonSerializer.SerializeToElement("Janus.Authorization.Policies.PolicyRegistry"));

        await Refusal.WriteAsync(context, fault, TestContext.Current.CancellationToken);

        context.Response.Body.Position = 0;

        using var reading = new StreamReader(context.Response.Body, Encoding.UTF8);
        string answered = await reading.ReadToEndAsync(TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Contains("\"code\":\"system.fault\"", answered, StringComparison.Ordinal);
        Assert.Contains("\"correlationId\":\"0HN7A2\"", answered, StringComparison.Ordinal);
        Assert.Contains("\"details\":{}", answered, StringComparison.Ordinal);
        Assert.DoesNotContain("PolicyRegistry", answered, StringComparison.Ordinal);
        Assert.DoesNotContain("authz.policy.unregistered", answered, StringComparison.Ordinal);
    }

    private static IEnumerable<ErrorCode> Catalogue() => typeof(ErrorCodes)
        .GetProperties(BindingFlags.Public | BindingFlags.Static)
        .Where(property => property.PropertyType == typeof(ErrorCode))
        .Select(property => (ErrorCode)property.GetValue(obj: null)!);
}
