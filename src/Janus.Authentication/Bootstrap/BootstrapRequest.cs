using System.Collections.Generic;
using Janus.Core.Configuration;

namespace Janus.Authentication.Bootstrap;

/// <summary>
/// What the person standing up a deployment names on the command line.
/// </summary>
/// <param name="Organization">The administrative organization's name.</param>
/// <param name="Email">The first administrator's personal email, as entered.</param>
/// <param name="Phone">The first administrator's phone number, as entered.</param>
/// <param name="Mailbox">
/// The corporate address the administrator's mailbox is provisioned at, or
/// <see langword="null"/> where the deployment integrates no mail server.
/// </param>
/// <param name="Named">
/// The written form of every value the deployment names, by its key, each already
/// read as its key admits.
/// </param>
/// <remarks>Implements OPS-BOOT-001 and INT-MAIL-006 AC1a.</remarks>
internal sealed record BootstrapRequest(
    string Organization,
    string Email,
    string Phone,
    string? Mailbox,
    IReadOnlyDictionary<ConfigurationKey, string> Named);
