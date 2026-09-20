using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The line between the library and the systems it talks to: no gateway is named in
/// the source, and an outbound payload is built in one place (INT-SMS-006,
/// INT-GEN-005, CONV-DESIGN-002).
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

    private static IEnumerable<string> Sources() =>
        Directory
            .EnumerateFiles(Path.Combine(Repository.Root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file =>
                !file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal));
}
