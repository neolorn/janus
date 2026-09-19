using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Janus.Storage.Tests;

/// <summary>
/// What the one context maps (CONV-DESIGN-003).
/// </summary>
/// <remarks>
/// The model is built from the design-time factory, which connects to nothing, so this
/// reads the mapping and not a database.
/// </remarks>
[Trait("kind", "unit")]
public sealed class ModelTests
{
    /// <summary>
    /// CONV-DESIGN-003 AC4: no domain entity type appears in the model. What is mapped
    /// is a persistence record, declared in this project beside its configuration, so
    /// an aggregate can never be loaded, tracked or written by the context itself.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_003_AC4_NoDomainEntityTypeAppearsInTheModel()
    {
        IEnumerable<Type> mapped = Model().GetEntityTypes().Select(entity => entity.ClrType);

        Assert.NotEmpty(mapped);
        Assert.All(mapped, type => Assert.Equal(typeof(JanusDbContext).Assembly, type.Assembly));
    }

    /// <summary>
    /// CONV-DESIGN-003: every table the library owns is mapped in the schema the library
    /// owns, so nothing of the host's is ever read or written through this context.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_003_EveryMappedTableIsInTheLibrarysOwnSchema() =>
        Assert.All(
            Model().GetEntityTypes(),
            entity => Assert.Equal(JanusDbContext.Schema, entity.GetSchema()));

    /// <summary>
    /// IDN-ORG-002 AC1: nothing in the schema says which kind of person a row is
    /// about. Staff are the members of the administrative organization, so a flag
    /// separating them from customers would be a second answer to a question the
    /// membership already answers.
    /// </summary>
    [Fact]
    public void IDN_ORG_002_AC1_NoColumnDistinguishesStaffFromCustomers()
    {
        string[] forbidden = ["staff", "customer", "employee", "internal", "external"];

        IEnumerable<string> columns = Model()
            .GetEntityTypes()
            .SelectMany(entity => entity.GetProperties())
            .Select(property => property.GetColumnName());

        Assert.All(columns, column => Assert.DoesNotContain(
            forbidden,
            word => column.Contains(word, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// REG-ACCT-001 AC2: every column the library maps is a field the item's table
    /// names, or one the chapter that table points to requires. A column that belongs
    /// to neither is a field the account grew without a rule for who fills it, so the
    /// set is written out here and compared whole.
    /// </summary>
    [Fact]
    public void REG_ACCT_001_AC2_NoFieldExistsOutsideTheGroupsTheTableNames()
    {
        string[] expected =
        [
            // Preferences: language and time zone in plain, the declared values under
            // the key, as the table's Key column says of each.
            "account_preferences.enc_values",
            "account_preferences.language",
            "account_preferences.subject",
            "account_preferences.time_zone",

            // Standing: the state and the two windows `01` section 4 gives it.
            "accounts.created_at",
            "accounts.deleting_by",
            "accounts.deleting_since",
            "accounts.state",
            "accounts.subject",
            "accounts.suspended_by",

            // Standing: the event record of IDN-AUD-001, with the attribute column
            // PRIV-RET-002 puts an event's personal field in.
            "audit_records.acting_subject",
            "audit_records.action",
            "audit_records.category",
            "audit_records.details",
            "audit_records.effective_subject",
            "audit_records.enc_details",
            "audit_records.id",
            "audit_records.occurred_at",
            "audit_records.organization",

            // Standing: the host-side progress of an erasure (IDN-LIFE-003b).
            "erasures.attempts",
            "erasures.reason",
            "erasures.requested_at",
            "erasures.status",
            "erasures.subject",

            // Identifiers: the backup setting of `10` section 5.17.
            "identifier_backup_settings.kind",
            "identifier_backup_settings.named",
            "identifier_backup_settings.rule",
            "identifier_backup_settings.subject",

            // Identifiers: the two forms and the fingerprint of IDN-ACCT-004 and
            // PRIV-RIGHT-005c, with the roles REG-IDENT-002 gives a row.
            "identifiers.added_at",
            "identifiers.canonicalisation_version",
            "identifiers.enc_canonical",
            "identifiers.enc_entered",
            "identifiers.fingerprint",
            "identifiers.identifier_id",
            "identifiers.is_locked",
            "identifiers.is_primary",
            "identifiers.kind",
            "identifiers.subject",
            "identifiers.verified_at",

            // Standing: the membership record of IDN-MEM-001, with its own beginning
            // and end.
            "memberships.created_at",
            "memberships.ended_at",
            "memberships.id",
            "memberships.organization",
            "memberships.subject",

            // The organization of IDN-ORG-001, with the deletion window of
            // IDN-ORG-003.
            "organizations.created_at",
            "organizations.deletion_requested_at",
            "organizations.erased_at",
            "organizations.id",
            "organizations.name",

            // Profile: the photo, in a table of its own (IDN-ATTR-003).
            "profile_photos.enc_image",
            "profile_photos.subject",
            "profile_photos.updated_at",

            // Profile: the three declared fields of REG-PROF-001, all under the key.
            "profiles.enc_date_of_birth",
            "profiles.enc_display_name",
            "profiles.enc_legal_name",
            "profiles.subject",

            // Not an account field: the runtime configuration of OPS-CFG-008.
            "settings.key",
            "settings.value",

            // Not an account field: the wrapped key of PRIV-RIGHT-005a, which every
            // column marked Key is written under.
            "subject_keys.format_marker",
            "subject_keys.key_version",
            "subject_keys.subject",
            "subject_keys.wrapped_key",
        ];

        IEnumerable<string> mapped = Model()
            .GetEntityTypes()
            .SelectMany(entity => entity.GetProperties()
                .Select(property => entity.GetTableName() + "." + property.GetColumnName()));

        Assert.Equal(expected, mapped.OrderBy(name => name, StringComparer.Ordinal));
    }

    private static IModel Model()
    {
        using JanusDbContext context = new DesignTimeContextFactory().CreateDbContext([]);

        return context.Model;
    }
}
