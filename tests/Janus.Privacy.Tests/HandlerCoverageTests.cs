using System;
using System.Collections.Generic;
using Janus.Core;
using Janus.Privacy.Tests.Exports;
using Janus.Privacy.Tests.Outbox;
using Xunit;

namespace Janus.Privacy.Tests;

/// <summary>
/// What a deployment has to have registered before it starts: a subject-event
/// subscriber for every sensitive type, an export contributor for every type holding
/// personal data, a purpose handler for every purpose resting on consent or on an
/// objectable basis, and a name of its own for each (PRIV-RIGHT-005b, PRIV-RIGHT-003,
/// PRIV-RIGHT-001a, IDN-LIFE-003a, LIB-HOST-001).
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
        Result outcome = Coverage([], [Handling()]).Validate();

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
        Result outcome = Coverage([Covering(Statement)], [Handling()]).Validate();

        Assert.True(outcome.Match(() => true, _ => false));
    }

    /// <summary>
    /// PRIV-RIGHT-005b AC3: sensitivity is what the requirement turns on, so a type
    /// declared without a category needs no subscriber and holds no deployment up.
    /// </summary>
    [Fact]
    public void PRIV_RIGHT_005b_AC3_ATypeThatIsNotSensitiveNeedsNoHandler()
    {
        AuthorizationDeclaration ordinary = new AuthorizationDeclarationBuilder()
            .RetentionFloor("identity", TimeSpan.FromDays(365))
            .LawfulBasis(new LawfulBasisDeclaration("agreement", "Agreement", true, true, false, false))
            .Permission("mailing:read")
            .Resource<Declaration.Mailing>("mailing", mailing => mailing
                .BelongsToOrganization()
                .Purpose("marketing", "agreement", data: ["identity"], subjects: ["customers"]))
            .Build();

        Result outcome = new HandlerCoverage(ordinary, [], [new PurposeHandlerInMemory("mailing", "marketing")], [])
            .Validate();

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
        Result outcome = Coverage([Covering(ResourceType.Parse("mailing"))], [Handling()]).Validate();

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
        Result outcome = Coverage(
            [Covering(Statement)],
            [new PurposeHandlerInMemory("consents", "marketing", "newsletter", "recommendations")]).Validate();

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
    /// PRIV-RIGHT-001a AC3, PRIV-CONS-008 (D-203): a purpose resting on consent is
    /// asked a handler of as an objectable one is, since a withdrawal erases what the
    /// host held solely for it, so a deployment registering none for it does not start,
    /// and the failure names the purpose.
    /// </summary>
    [Fact]
    public void PRIV_RIGHT_001a_AC3_AConsentBasedPurposeWithNoRegisteredHandlerFailsStartup()
    {
        Result outcome = Coverage(
            [Covering(Statement)],
            [new PurposeHandlerInMemory("objections", "security", "marketing", "recommendations")]).Validate();

        Assert.Equal(
            ErrorCodes.StartupDeclarationMissing,
            outcome.Match(() => (ErrorCode?)null, failure => failure.Code));
        Assert.Equal(
            "newsletter",
            outcome.Match(
                () => null,
                failure => failure.Details["handler"].GetString()));
    }

    /// <summary>
    /// PRIV-RIGHT-001a AC3: every purpose that needs a handler may be named by a handler
    /// of its own, and a deployment whose handlers together name each starts.
    /// </summary>
    [Fact]
    public void PRIV_RIGHT_001a_AC3_HandlersTogetherNamingEveryPurposeStart()
    {
        Result outcome = Coverage(
            [Covering(Statement)],
            [
                new PurposeHandlerInMemory("objections", "security"),
                new PurposeHandlerInMemory("consents", "marketing", "newsletter", "recommendations"),
            ]).Validate();

        Assert.True(outcome.Match(() => true, _ => false));
    }

    /// <summary>
    /// IDN-LIFE-003a AC7, DR-016 AC2: a subscriber registered under the name the erasure
    /// ledger's confirmation is recorded under would take the ledger's line for its own
    /// work, so the deployment does not start, and the failure names it.
    /// </summary>
    [Fact]
    public void IDN_LIFE_003a_AC7_ASubscriberUnderTheLedgersNameFailsStartup()
    {
        Result outcome = Coverage(
            [Covering(Statement), new SubscriberInMemory("erasure-ledger", required: true)],
            [Handling()]).Validate();

        Assert.Equal(
            ErrorCodes.StartupSubscriberName,
            outcome.Match(() => (ErrorCode?)null, failure => failure.Code));
        Assert.Equal(
            "erasure-ledger",
            outcome.Match(
                () => null,
                failure => failure.Details["handler"].GetString()));
    }

    /// <summary>
    /// IDN-LIFE-003a AC7: two subscribers under one name would share one confirmation,
    /// so the second would never be offered an event the first had confirmed; the
    /// deployment does not start.
    /// </summary>
    [Fact]
    public void IDN_LIFE_003a_AC7_TwoSubscribersUnderOneNameFailStartup()
    {
        Result outcome = Coverage(
            [Covering(Statement), new SubscriberInMemory("host", required: false)],
            [Handling()]).Validate();

        Assert.Equal(
            ErrorCodes.StartupSubscriberName,
            outcome.Match(() => (ErrorCode?)null, failure => failure.Code));
        Assert.Equal(
            "host",
            outcome.Match(
                () => null,
                failure => failure.Details["handler"].GetString()));
    }

    /// <summary>
    /// IDN-LIFE-003a AC7 (D-203): a purpose handler's confirmation is recorded under its
    /// name as a subscriber's is, so a handler under a subscriber's name, under another
    /// handler's or under the erasure ledger's fails startup, naming it.
    /// </summary>
    /// <param name="taken">The name the second registration takes.</param>
    [Theory]
    [InlineData("host")]
    [InlineData("purposes")]
    [InlineData("erasure-ledger")]
    public void IDN_LIFE_003a_AC7_APurposeHandlerUnderATakenNameFailsStartup(string taken)
    {
        Result outcome = Coverage(
            [Covering(Statement)],
            [Handling(), new PurposeHandlerInMemory(taken, "security")]).Validate();

        Assert.Equal(
            ErrorCodes.StartupSubscriberName,
            outcome.Match(() => (ErrorCode?)null, failure => failure.Code));
        Assert.Equal(
            taken,
            outcome.Match(
                () => null,
                failure => failure.Details["handler"].GetString()));
    }

    /// <summary>
    /// PRIV-RIGHT-001a AC3: a purpose resting neither on consent nor on an objectable
    /// basis is asked no handler of, so naming the others is enough.
    /// </summary>
    [Fact]
    public void PRIV_RIGHT_001a_AC3_APurposeOnAnotherBasisNeedsNoHandler()
    {
        Result outcome = Coverage([Covering(Statement)], [Handling()]).Validate();

        Assert.True(outcome.Match(() => true, _ => false));
        Assert.DoesNotContain("performance", Handling().Purposes);
    }

    /// <summary>
    /// PRIV-RIGHT-003 AC6: the deployment declares its statement sensitive and registers
    /// no export contributor that covers it, so it does not start, and the failure names
    /// the type.
    /// </summary>
    [Fact]
    public void PRIV_RIGHT_003_AC6_ASensitiveTypeNoContributorCoversFailsStartup()
    {
        Result outcome = Coverage(
            [Covering(Statement)],
            [new PurposeHandlerInMemory("security")],
            [new ExportContributorInMemory("mailings") { Covers = [ResourceType.Parse("mailing")] }]).Validate();

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
    /// PRIV-RIGHT-003 AC6: a type that is not sensitive but declares an encrypted field
    /// holds personal data an export must carry, so with no contributor covering it the
    /// deployment does not start; once one covers it, it does.
    /// </summary>
    [Fact]
    public void PRIV_RIGHT_003_AC6_ATypeWithAnEncryptedFieldNoContributorCoversFailsStartup()
    {
        AuthorizationDeclaration encrypting = new AuthorizationDeclarationBuilder()
            .RetentionFloor("identity", TimeSpan.FromDays(365))
            .LawfulBasis(new LawfulBasisDeclaration("contract", "Contract", false, false, false, false))
            .Permission("mailing:read")
            .Resource<Declaration.Mailing>("mailing", mailing => mailing
                .BelongsToOrganization()
                .Purpose("delivery", "contract", data: ["identity"], subjects: ["customers"])
                .Encrypted(row => row.Address, row => row.Recipient, "identity"))
            .Build();

        Result uncovered = new HandlerCoverage(encrypting, [], [], []).Validate();
        Result covered = new HandlerCoverage(
            encrypting,
            [],
            [],
            [new ExportContributorInMemory("mailings") { Covers = [ResourceType.Parse("mailing")] }]).Validate();

        Assert.Equal(
            ErrorCodes.StartupDeclarationMissing,
            uncovered.Match(() => (ErrorCode?)null, failure => failure.Code));
        Assert.Equal(
            "mailing",
            uncovered.Match(
                () => null,
                failure => failure.Details["handler"].GetString()));
        Assert.True(covered.Match(() => true, _ => false));
    }

    private static SubscriberInMemory Covering(ResourceType type) =>
        new("host", required: true) { Covers = [type] };

    // One handler naming every purpose of the declaration that rests on consent or on
    // an objectable basis.
    private static PurposeHandlerInMemory Handling() =>
        new("purposes", "marketing", "newsletter", "recommendations", "security");

    private static HandlerCoverage Coverage(
        IEnumerable<ISubjectEventSubscriber> subscribers,
        IEnumerable<IPurposeHandler> handlers) =>
        Coverage(subscribers, handlers, [new ExportContributorInMemory("statements") { Covers = [Statement] }]);

    private static HandlerCoverage Coverage(
        IEnumerable<ISubjectEventSubscriber> subscribers,
        IEnumerable<IPurposeHandler> handlers,
        IEnumerable<IExportContributor> contributors) =>
        new(Declaration.Declared().Build(), subscribers, handlers, contributors);
}
