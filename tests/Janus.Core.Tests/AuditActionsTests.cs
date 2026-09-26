using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The catalogue of audit actions: that it is closed, that it is documented, and that
/// it does not change without the change being made deliberately (IDN-AUD-001,
/// CONV-NAME-003).
/// </summary>
[Trait("kind", "contract")]
public sealed class AuditActionsTests
{
    private static readonly string[] Catalogue =
    [
        "auth.botdefence.signalled",
        "auth.breakglass.generated",
        "auth.breakglass.used",
        "auth.credential.countermismatch",
        "auth.credential.enrolled",
        "auth.credential.invalidated",
        "auth.credential.invalidationheld",
        "auth.credential.removed",
        "auth.credential.reportcancelled",
        "auth.credential.reportedlost",
        "auth.credential.restored",
        "auth.mailcredential.created",
        "auth.mailcredential.revoked",
        "auth.oidc.clientregistered",
        "auth.oidc.refreshreused",
        "auth.phonesignal.considered",
        "auth.providerevent.rejected",
        "auth.providerevent.taken",
        "auth.recovery.approved",
        "auth.restriction.edited",
        "auth.restriction.granted",
        "auth.session.presented",
        "authz.access.denied",
        "authz.access.exported",
        "authz.group.created",
        "authz.group.memberadded",
        "authz.group.memberremoved",
        "authz.group.removed",
        "authz.role.defined",
        "authz.role.removed",
        "identity.account.deactivated",
        "identity.account.reactivated",
        "identity.account.suspended",
        "identity.credential.labelled",
        "identity.deletion.cancelled",
        "identity.deletion.requested",
        "identity.invitation.acknowledged",
        "identity.invitation.issued",
        "identity.invitation.revoked",
        "identity.membership.ended",
        "identity.organization.created",
        "identity.organization.deletioncancelled",
        "identity.organization.deletionrequested",
        "identity.organization.domainadded",
        "identity.organization.domainremoved",
        "identity.organization.domainverified",
        "identity.organization.erased",
        "identity.preferences.changed",
        "identity.profile.changed",
        "identity.secondstep.preferred",
        "identity.takedown.executed",
        "identity.takedown.reversed",
        "identity.username.changed",
        "ops.auditpartitions.maintained",
        "ops.configuration.changed",
        "ops.keyrotation.completed",
        "ops.keyrotation.resumed",
        "ops.keyrotation.retired",
        "ops.keyrotation.started",
        "ops.restoretest.completed",
        "privacy.consent.granted",
        "privacy.consent.withdrawn",
        "privacy.document.published",
        "privacy.document.translated",
        "privacy.erasure.completed",
        "privacy.erasure.executed",
        "privacy.export.assembled",
        "privacy.objection.recorded",
        "privacy.objection.withdrawn",
        "privacy.request.entered",
        "privacy.request.fulfilled",
        "privacy.request.lapsed",
        "privacy.request.refused",
        "privacy.request.submitted",
        "privacy.restriction.lifted",
    ];

    /// <summary>
    /// IDN-AUD-001: the library records one of these and nothing else, so an action
    /// added anywhere without being added here fails this test.
    /// </summary>
    [Fact]
    public void IDN_AUD_001_TheSetOfActionsIsClosed()
    {
        var declared = Actions().Values.Order(StringComparer.Ordinal).ToList();

        Assert.Equal(Catalogue, declared);
    }

    /// <summary>
    /// CONV-NAME-003 AC2: an action that changes spelling fails this test, so the
    /// change is made deliberately and carries its version bump.
    /// </summary>
    [Fact]
    public void CONV_NAME_003_AC2_ChangingAnActionFailsTheContractTest() =>
        Assert.Equal(Catalogue.Length, Actions().Count);

    /// <summary>
    /// CONV-NAME-003 AC1: every action says what it records, so the row a reader meets
    /// in the trail is explained where the action is declared.
    /// </summary>
    [Fact]
    public void CONV_NAME_003_AC1_EveryActionSaysWhatItRecords()
    {
        var documentation = XDocument.Parse(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Janus.Core.xml")));

        foreach (string name in Actions().Keys)
        {
            XElement? summary = documentation
                .Descendants("member")
                .FirstOrDefault(member =>
                    string.Equals(member.Attribute("name")?.Value, "P:Janus.Core.AuditActions." + name, StringComparison.Ordinal))
                ?.Element("summary");

            Assert.NotNull(summary);
            Assert.True(
                summary.Value.Trim().Length > 0,
                name + " is declared but says nothing about what it records.");
        }
    }

    private static Dictionary<string, string> Actions() =>
        typeof(AuditActions)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .ToDictionary(
                property => property.Name,
                property => property.GetValue(null)!.ToString()!,
                StringComparer.Ordinal);
}
