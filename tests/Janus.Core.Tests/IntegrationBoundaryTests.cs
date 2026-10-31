using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The line between the library and the systems it talks to: no gateway or mail
/// server is named in the source and none is called to send, a message handed to the
/// host is built in one place, and the one database the library reaches is its own
/// (INT-SMS-006, INT-GEN-005, CONV-DESIGN-002, INT-MAIL-001, INT-MAIL-003,
/// INT-MAIL-008, LIB-EXT-001).
/// </summary>
[Trait("kind", "contract")]
public sealed class IntegrationBoundaryTests
{
    // The gateways a host's messaging might send through. None of them is the
    // library's business: the host sends, the library names none.
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

    // The caches a deployment might one day put in front of its database, and the
    // clients that reach them. Version 1 has no cache and no extension point for one
    // (INF-CACHE-001), and the core contract names none.
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
    // credential from through the secret source. "AWS" is read apart, as a whole word
    // in capitals, because it is in "draws" and "withdraws".
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

    // The one vendor name that is also the inside of ordinary words.
    private static readonly Regex Aws = new(@"\bAWS\b", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    // The one schema the library owns, and the catalogue PostgreSQL answers its own
    // questions from.
    private static readonly string[] OwnSchemas = ["identity", "pg_catalog"];

    // A relation named in a statement, schema first.
    private static readonly Regex Relation = new(
        @"\b(?:FROM|JOIN|INTO|UPDATE|TABLE|REFERENCES)\s+""?([a-z_]+)""?\.""?[a-z_]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    // The payload that crosses the boundary to the host's notification handler, built
    // at the one mapping site and nowhere else.
    private static readonly string[] Payloads = ["new SendRequest("];

    private static readonly string[] Mapping = ["SendDelivery.cs"];

    // What sending a message itself would take: the framework's mail client, or a
    // client library of a mail or text provider.
    private static readonly string[] Senders = ["System.Net.Mail", "SmtpClient", "MailKit", "MimeKit"];

    private static readonly string[] CoreProjects =
    [
        "Janus.Core",
        "Janus.Identity",
        "Janus.Authentication",
        "Janus.Authorization",
        "Janus.Privacy",
    ];

    /// <summary>
    /// INT-SMS-006 AC1 and INT-MAIL-008 AC2: no gateway or provider is named anywhere
    /// in the library and nothing in it could send a message itself, so every message
    /// leaves through the host's notification handler.
    /// </summary>
    [Fact]
    public void INT_SMS_006_AC1_NoGatewayOrSenderIsCalledByTheLibrary()
    {
        IEnumerable<string> naming = Sources()
            .Where(file => Gateways.Any(gateway =>
                    File.ReadAllText(file).Contains(gateway, StringComparison.OrdinalIgnoreCase))
                || Senders.Any(sender => File.ReadAllText(file).Contains(sender, StringComparison.Ordinal)));

        Assert.Empty(naming);
    }

    /// <summary>
    /// LIB-EXT-001 AC3: no product of any extension point is named in a core
    /// namespace, <c>Janus.Core</c> or an area project: no gateway or mail sender,
    /// no mail server and no secrets manager, and no cache, which version 1 does not
    /// have (INF-CACHE-001). Google and Apple are not searched for, since they are factors of the
    /// catalogue chapter 02 fixes rather than a product behind an extension point, and
    /// neither is PostgreSQL, which LIB-API-004 has the contract name.
    /// </summary>
    [Fact]
    public void LIB_EXT_001_AC3_NoProviderNameAppearsInTheCoreNamespace()
    {
        string[] products = [.. Gateways, .. MailServers, .. Caches, .. SecretSources];
        string[] core = [.. CoreNamespaces()];

        IEnumerable<string> naming = core
            .Where(file => products.Any(product =>
                    File.ReadAllText(file).Contains(product, StringComparison.OrdinalIgnoreCase))
                || Aws.IsMatch(File.ReadAllText(file)))
            .Select(Path.GetFileName)
            .Select(name => name!);

        Assert.NotEmpty(core);
        Assert.Empty(naming);
    }

    /// <summary>
    /// INT-GEN-005 AC2: no call site builds the message handed to the host, so the
    /// fields that leave the library are decided in one place and can be tested there.
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
    /// INT-MAIL-008 AC1: no mail server is named in a core namespace, <c>Janus.Core</c>
    /// or an area project, so the one hosting the staff mailboxes is a deployment's
    /// registration. The shipped adapter in <c>Janus.Hosting</c> speaks the capability
    /// INT-MAIL-001 names, and is outside them.
    /// </summary>
    [Fact]
    public void INT_MAIL_008_AC1_NoMailServerIsNamedInTheCoreNamespace()
    {
        string[] core = [.. CoreNamespaces()];

        IEnumerable<string> naming = core
            .Where(file => MailServers.Any(server =>
                File.ReadAllText(file).Contains(server, StringComparison.OrdinalIgnoreCase)));

        Assert.NotEmpty(core);
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

    private static string[] Naming(string text) =>
    [
        .. Sources()
            .Where(file => File.ReadAllText(file).Contains(text, StringComparison.Ordinal))
            .Where(file => !file.Contains(Path.DirectorySeparatorChar + "Migrations" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .Select(name => name!)
            .Order(StringComparer.Ordinal),
    ];

    // Chapter 07: a core namespace is Janus.Core's or an area project's.
    private static IEnumerable<string> CoreNamespaces() =>
        Sources().Where(file => CoreProjects.Any(project => file.Contains(
            Path.DirectorySeparatorChar + project + Path.DirectorySeparatorChar,
            StringComparison.Ordinal)));

    private static IEnumerable<string> Sources() =>
        Directory
            .EnumerateFiles(Path.Combine(Repository.Root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file =>
                !file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal));
}
