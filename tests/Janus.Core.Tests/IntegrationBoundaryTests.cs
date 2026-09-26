using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The line between the library and the systems it talks to: no gateway or mail
/// server is named in the source, an outbound payload is built in one place, and the
/// one database the library reaches is its own (INT-SMS-006, INT-GEN-005,
/// CONV-DESIGN-002, INT-MAIL-001, INT-MAIL-003, INT-MAIL-008, INT-MAIL-009,
/// LIB-EXT-001).
/// </summary>
[Trait("kind", "contract")]
public sealed class IntegrationBoundaryTests
{
    // The gateways a deployment might put behind the transport ports. None of them
    // is the library's business: a host registers one, the library names none.
    private static readonly string[] Gateways =
    [
        "Twilio",
        "Vonage",
        "Nexmo",
        "MessageBird",
        "Plivo",
        "Infobip",
        "Sinch",
        "Unifonic",
        "Clickatell",
        "Kaleyra",
        "Telnyx",
        "SMSMisr",
        "VictoryLink",
        "Mailgun",
        "SendGrid",
        "Postmark",
        "Mailchimp",
    ];

    // The mail servers a deployment might host its staff mailboxes on. The library
    // reaches one through IMailServer and names none.
    private static readonly string[] MailServers =
    [
        "Stalwart",
        "Postfix",
        "Dovecot",
        "Zimbra",
        "Mailcow",
    ];

    // The caches a deployment might put behind the cache extension point, and the
    // clients that reach them. Chapter 07 ships one of them by default, and the core
    // contract still names none.
    private static readonly string[] Caches =
    [
        "Redis",
        "StackExchange",
        "Valkey",
        "Memcached",
        "Garnet",
        "KeyDB",
        "Dragonfly",
        "Hazelcast",
    ];

    // The secrets managers a deployment might read its keys and the maintenance
    // credential from through the secret source. "AWS" is left out because it is in
    // "draws" and "withdraws", and the vendor's other names stand for it.
    private static readonly string[] SecretSources =
    [
        "HashiCorp",
        "OpenBao",
        "KeyVault",
        "Key Vault",
        "Azure",
        "Amazon",
        "AWSSDK",
        "Google.Cloud",
        "GoogleCloud",
        "SecretManager",
        "Infisical",
        "Doppler",
        "1Password",
        "Bitwarden",
        "CyberArk",
        "Akeyless",
        "Delinea",
        "Thycotic",
    ];

    // The one schema the library owns, and the catalogue PostgreSQL answers its own
    // questions from.
    private static readonly string[] OwnSchemas = ["identity", "pg_catalog"];

    // A relation named in a statement, schema first.
    private static readonly Regex Relation = new(
        @"\b(?:FROM|JOIN|INTO|UPDATE|TABLE|REFERENCES)\s+""?([a-z_]+)""?\.""?[a-z_]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    // The payloads that cross an outbound boundary, each built at the one mapping
    // site and nowhere else.
    private static readonly string[] Payloads = ["new MailMessage(", "new SmsMessage("];

    private static readonly string[] Mapping = ["SendingService.cs"];

    /// <summary>
    /// INT-SMS-006 AC1: no provider name appears anywhere in the library, so the
    /// gateway is the deployment's choice and replacing it is a registration.
    /// </summary>
    [Fact]
    public void INT_SMS_006_AC1_NoProviderNameAppearsInTheLibrary()
    {
        IEnumerable<string> naming = Sources()
            .Where(file => Gateways.Any(gateway =>
                File.ReadAllText(file).Contains(gateway, StringComparison.OrdinalIgnoreCase)));

        Assert.Empty(naming);
    }

    /// <summary>
    /// LIB-EXT-001 AC3: no product of any extension point is named in the core
    /// contract: no gateway or mail transport, no mail server, no cache and no secrets
    /// manager. Google and Apple are not searched for, since they are factors of the
    /// catalogue chapter 02 fixes rather than a product behind an extension point, and
    /// neither is PostgreSQL, which LIB-API-004 has the contract name.
    /// </summary>
    [Fact]
    public void LIB_EXT_001_AC3_NoProviderNameAppearsInTheCoreNamespace()
    {
        string[] products = [.. Gateways, .. MailServers, .. Caches, .. SecretSources];
        string[] core =
        [
            .. Sources().Where(file => file.Contains(
                Path.DirectorySeparatorChar + "Janus.Core" + Path.DirectorySeparatorChar,
                StringComparison.Ordinal)),
        ];

        IEnumerable<string> naming = core
            .Where(file => products.Any(product =>
                File.ReadAllText(file).Contains(product, StringComparison.OrdinalIgnoreCase)))
            .Select(Path.GetFileName)
            .Select(name => name!);

        Assert.NotEmpty(core);
        Assert.Empty(naming);
    }

    /// <summary>
    /// INT-GEN-005 AC2: no call site builds an outbound payload, so the fields that
    /// leave the deployment are decided in one place and can be tested there.
    /// </summary>
    [Fact]
    public void INT_GEN_005_AC2_NoCallSiteBuildsAnOutboundPayload()
    {
        IEnumerable<string> building = Sources()
            .Where(file => !Mapping.Contains(Path.GetFileName(file), StringComparer.Ordinal))
            .Where(file => Payloads.Any(payload =>
                File.ReadAllText(file).Contains(payload, StringComparison.Ordinal)));

        Assert.Empty(building);
    }

    /// <summary>
    /// INT-MAIL-008 AC1: no mail server is named anywhere in the library, so the one
    /// hosting the staff mailboxes is a deployment's registration.
    /// </summary>
    [Fact]
    public void INT_MAIL_008_AC1_NoMailServerIsNamedInTheLibrary()
    {
        IEnumerable<string> naming = Sources()
            .Where(file => MailServers.Any(server =>
                File.ReadAllText(file).Contains(server, StringComparison.OrdinalIgnoreCase)));

        Assert.Empty(naming);
    }

    /// <summary>
    /// INT-MAIL-001 AC1: the library opens one database, the one the host names when
    /// it registers the library: one context, configured in the registration and in
    /// the design-time factory the migration tooling uses, and nowhere else.
    /// </summary>
    [Fact]
    public void INT_MAIL_001_AC1_NoCodeOpensADatabaseButTheLibrarysOwn()
    {
        Assert.Equal(
            ["DesignTimeContextFactory.cs", "StorageRegistration.cs"],
            Naming("UseNpgsql("));
        Assert.Equal(["StoreContext.cs"], Naming(": DbContext("));
        Assert.Empty(Naming("NpgsqlDataSource"));
    }

    /// <summary>
    /// INT-MAIL-003 AC1: every relation a statement of the library names is in the
    /// library's own schema or PostgreSQL's catalogue; a host's relation reaches a
    /// statement only from the host's declaration, and a mail server's never does.
    /// </summary>
    [Fact]
    public void INT_MAIL_003_AC1_NoStatementNamesARelationOutsideTheLibrarysSchema()
    {
        string[] schemas =
        [
            .. Sources()
                .SelectMany(file => Relation.Matches(File.ReadAllText(file)))
                .Select(match => match.Groups[1].Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase),
        ];

        Assert.NotEmpty(schemas);
        Assert.All(schemas, schema => Assert.Contains(schema, OwnSchemas, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// INT-MAIL-009 AC2: no source reaches both mailbox hosting and outbound delivery,
    /// so replacing the one leaves every user of the other as it was.
    /// </summary>
    [Fact]
    public void INT_MAIL_009_AC2_NoSourceReachesBothMailboxHostingAndDelivery()
    {
        string[] hosting = Naming("IMailServer");
        string[] delivery = Naming("IMailTransport");

        Assert.NotEmpty(hosting);
        Assert.NotEmpty(delivery);
        Assert.Empty(hosting.Intersect(delivery, StringComparer.Ordinal));
    }

    private static string[] Naming(string text) =>
    [
        .. Sources()
            .Where(file => File.ReadAllText(file).Contains(text, StringComparison.Ordinal))
            .Where(file => !file.Contains(Path.DirectorySeparatorChar + "Migrations" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .Select(name => name!)
            .Order(StringComparer.Ordinal),
    ];

    private static IEnumerable<string> Sources() =>
        Directory
            .EnumerateFiles(Path.Combine(Repository.Root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file =>
                !file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal));
}
