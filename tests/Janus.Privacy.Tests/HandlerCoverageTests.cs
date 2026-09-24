using System.Collections.Generic;
using Janus.Core;
using Xunit;

namespace Janus.Privacy.Tests;

/// <summary>
/// What a deployment has to have registered before it starts: a subject-event
/// subscriber for every sensitive type, and a handler for every objectable purpose
/// (PRIV-RIGHT-005b, PRIV-RIGHT-001a, LIB-HOST-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class HandlerCoverageTests
{
    private static readonly ResourceType Statement = ResourceType.Parse("statement");

    /// <summary>
    /// PRIV-RIGHT-005b AC3: the deployment declares its statement sensitive and
    /// registers nothing that covers it, so it does not start, and the failure names
    /// the type whose handler is missing.
    /// </summary>
    [Fact]
    public void PRIV_RIGHT_005b_AC3_ASensitiveTypeWithNoRegisteredHandlerFailsStartup()
    {
        Result outcome = Coverage([], [new PurposeHandlerInMemory("security")]).Validate();

        Assert.Equal(
            ErrorCodes.StartupDeclarationMissing,
            outcome.Match(() => (ErrorCode?)null, failure => failure.Code));
        Assert.Equal(
            "statement",
            outcome.Match(
                () => null,
                failure => failure.Details["handler"].GetString()));
    }

    /// <summary>
    /// PRIV-RIGHT-005b AC3: a subscriber naming the sensitive type is what the check
    /// is looking for, and the deployment starts.
    /// </summary>
    [Fact]
    public void PRIV_RIGHT_005b_AC3_ASensitiveTypeASubscriberCoversStarts()
    {
        Result outcome = Coverage(
            [Covering(Statement)],
            [new PurposeHandlerInMemory("security")]).Validate();

        Assert.True(outcome.Match(() => true, _ => false));
    }

    /// <summary>
    /// PRIV-RIGHT-005b AC3: sensitivity is what the requirement turns on, so a type
    /// declared without a category needs no handler and holds no deployment up.
    /// </summary>
    [Fact]
    public void PRIV_RIGHT_005b_AC3_ATypeThatIsNotSensitiveNeedsNoHandler()
    {
        AuthorizationDeclaration ordinary = new AuthorizationDeclarationBuilder()
            .LawfulBasis(new LawfulBasisDeclaration("agreement", true, true, false, false))
            .Permission("mailing:read")
            .Resource<Declaration.Mailing>("mailing", mailing => mailing
                .BelongsToOrganization()
                .Purpose("marketing", "agreement", data: ["identity"], subjects: ["customers"]))
            .Build();

        Result outcome = new HandlerCoverage(ordinary, [], []).Validate();

        Assert.True(outcome.Match(() => true, _ => false));
    }

    /// <summary>
    /// IDN-LIFE-003a AC6: the library names no subscriber of its own, so what the
    /// check reads is what the host registered, and a host that registered none for
    /// a sensitive type is stopped.
    /// </summary>
    [Fact]
    public void IDN_LIFE_003a_AC6_AMissingHandlerFailsStartup()
    {
        Result outcome = Coverage(
            [Covering(ResourceType.Parse("mailing"))],
            [new PurposeHandlerInMemory("security")]).Validate();

        Assert.False(outcome.Match(() => true, _ => false));
    }

    /// <summary>
    /// PRIV-RIGHT-001a AC3: the deployment declares a purpose on an objectable basis
    /// and registers no handler for it, so it does not start, as for erasure and
    /// restriction.
    /// </summary>
    [Fact]
    public void PRIV_RIGHT_001a_AC3_AnObjectablePurposeWithNoRegisteredHandlerFailsStartup()
    {
        Result outcome = Coverage([Covering(Statement)], []).Validate();

        Assert.Equal(
            ErrorCodes.StartupDeclarationMissing,
            outcome.Match(() => (ErrorCode?)null, failure => failure.Code));
        Assert.Equal(
            "security",
            outcome.Match(
                () => null,
                failure => failure.Details["handler"].GetString()));
    }

    /// <summary>
    /// PRIV-RIGHT-001a AC3: a purpose on a basis that is not objectable is asked no
    /// handler of, so registering the objectable one is enough.
    /// </summary>
    [Fact]
    public void PRIV_RIGHT_001a_AC3_APurposeOnANonObjectableBasisNeedsNoHandler()
    {
        Result outcome = Coverage(
            [Covering(Statement)],
            [new PurposeHandlerInMemory("security")]).Validate();

        Assert.True(outcome.Match(() => true, _ => false));
    }

    private static Outbox.SubscriberInMemory Covering(ResourceType type) =>
        new("host", required: true) { Covers = [type] };

    private static HandlerCoverage Coverage(
        IEnumerable<ISubjectEventSubscriber> subscribers,
        IEnumerable<IPurposeHandler> handlers) =>
        new(Declaration.Declared().Build(), subscribers, handlers);
}
