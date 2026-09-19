using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Janus.Core;
using Janus.Identity.Accounts;
using Janus.Identity.Audit;
using Janus.Identity.Organizations;
using Xunit;

namespace Janus.Identity.Tests.Audit;

/// <summary>
/// What an audit record carries, and what no port may do to one (IDN-AUD-001,
/// PRIV-RET-002, PRIV-RET-004, IDN-PRIN-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class AuditRecordTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private static readonly AuditRecordId Id =
        new(Guid.Parse("55555555-5555-4555-8555-555555555555"));

    private static readonly SubjectId Ahmed =
        new(Guid.Parse("11111111-1111-4111-8111-111111111111"));

    private static readonly SubjectId Mona =
        new(Guid.Parse("22222222-2222-4222-8222-222222222222"));

    /// <summary>
    /// IDN-AUD-001 AC1: the record names who acted and whose identity the action was
    /// taken under, which are two fields and not one.
    /// </summary>
    [Fact]
    public void IDN_AUD_001_AC1_TheRecordNamesBothIdentities()
    {
        var record = AuditRecord.Of(
            Id,
            AuditCategory.Security,
            AuditAction.Parse("identity.account.suspended"),
            Noon,
            Mona,
            Ahmed,
            organization: null);

        Assert.Equal(Mona, record.ActingSubject);
        Assert.Equal(Ahmed, record.EffectiveSubject);
    }

    /// <summary>
    /// IDN-AUD-001 AC2: the record carries the instant the event occurred. Nothing on
    /// it says when it was written, so a queued event cannot record a write time by
    /// accident.
    /// </summary>
    [Fact]
    public void IDN_AUD_001_AC2_TheRecordedInstantIsTheOneItOccurredAt()
    {
        var record = AuditRecord.Of(
            Id,
            AuditCategory.Security,
            AuditAction.Parse("identity.account.suspended"),
            Noon,
            Ahmed,
            Ahmed,
            organization: null);

        Assert.Equal(Noon, record.OccurredAt);
        Assert.DoesNotContain(
            typeof(AuditRecord).GetProperties(),
            property => property.Name.Contains("Written", StringComparison.Ordinal));
    }

    /// <summary>
    /// IDN-AUD-001: an event that belongs to an organization names it, and one that
    /// belongs to none names none.
    /// </summary>
    [Fact]
    public void IDN_AUD_001_TheOrganizationIsThereWhereOneApplies()
    {
        var acme = new OrganizationId(Guid.Parse("33333333-3333-4333-8333-333333333333"));

        Assert.Equal(acme, Recorded(acme).Organization);
        Assert.Null(Recorded(organization: null).Organization);
    }

    /// <summary>
    /// PRIV-RET-004 AC1: what happened is a code, and the fields beside it are
    /// structured. A record carries no sentence to translate.
    /// </summary>
    [Fact]
    public void PRIV_RET_004_AC1_WhatHappenedIsACodeAndNotASentence()
    {
        Assert.Throws<ArgumentException>(() => AuditAction.Parse("The account was suspended"));
        Assert.Throws<ArgumentException>(() => AuditAction.Parse("Identity.Account.Suspended"));
        Assert.Equal(
            "identity.account.suspended",
            AuditAction.Parse("identity.account.suspended").ToString());
    }

    /// <summary>
    /// PRIV-RET-002: an event that has to record an attribute holds it apart from the
    /// structured fields, because that is the one part of the row the subject's key
    /// covers.
    /// </summary>
    [Fact]
    public void PRIV_RET_002_AnAttributeIsHeldApartFromTheStructuredFields()
    {
        var record = AuditRecord.Of(
            Id,
            AuditCategory.Security,
            AuditAction.Parse("identity.identifier.added"),
            Noon,
            Ahmed,
            Ahmed,
            organization: null,
            details: Fields(("kind", "email")),
            personalDetails: Fields(("added", "ahmed@example.com")));

        Assert.Equal("email", record.Details["kind"].GetString());
        Assert.False(record.Details.ContainsKey("added"));
        Assert.Equal("ahmed@example.com", record.PersonalDetails["added"].GetString());
    }

    /// <summary>
    /// IDN-PRIN-003 AC1: no port removes a record of something that happened. Accounts,
    /// organizations, memberships and audit records are never taken out of the
    /// database; what looks like removal is a state or an erasure.
    /// </summary>
    [Fact]
    public void IDN_PRIN_003_AC1_NoPortRemovesARecordOfSomethingThatHappened()
    {
        Type[] ports =
        [
            typeof(IAccountStore),
            typeof(IOrganizationStore),
            typeof(IMembershipStore),
            typeof(IAuditStore),
        ];

        IEnumerable<string> removing = ports
            .SelectMany(port => port.GetMethods())
            .Select(method => method.DeclaringType!.Name + "." + method.Name)
            .Where(name => name.Contains("Remove", StringComparison.Ordinal)
                || name.Contains("Delete", StringComparison.Ordinal));

        Assert.Empty(removing);
    }

    /// <summary>
    /// The audit port offers an append and a read and nothing that changes a record.
    /// </summary>
    [Fact]
    public void PRIV_RET_002_AC1_TheAuditPortOffersNoWriteButAnAppend()
    {
        IEnumerable<string> methods = typeof(IAuditStore)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(method => method.Name);

        Assert.Equal(["AppendAsync", "FindBySubjectAsync"], methods.Order(StringComparer.Ordinal));
    }

    private static Dictionary<string, JsonElement> Fields(params (string Name, string Value)[] fields)
    {
        var document = new Dictionary<string, JsonElement>(fields.Length, StringComparer.Ordinal);

        foreach ((string name, string value) in fields)
        {
            document[name] = JsonSerializer.SerializeToElement(value);
        }

        return document;
    }

    private static AuditRecord Recorded(OrganizationId? organization) =>
        AuditRecord.Of(
            Id,
            AuditCategory.Security,
            AuditAction.Parse("identity.membership.began"),
            Noon,
            Ahmed,
            Ahmed,
            organization);
}
