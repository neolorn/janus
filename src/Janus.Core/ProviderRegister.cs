using System.Collections.Generic;
using Janus.Core.Configuration;

namespace Janus.Core;

/// <summary>
/// The provider register of chapter 05 section 6: the three processors and recipients
/// the library's own processing makes true of every deployment that configures the
/// integration each describes.
/// </summary>
/// <remarks>
/// Implements PRIV-ROPA-002. The records of processing apply each row while its
/// integration is configured, whether or not the deployment declares it, and a row the
/// deployment declares under the same name stands in its place. Every processor of the
/// host's own business, the providers that deliver its messages among them, is the host's
/// declaration; the library ships no such row (D-225). Every
/// row here carries no agreement reference and is flagged until the deployment gives
/// it one.
/// </remarks>
public static class ProviderRegister
{
    /// <summary>
    /// The rows chapter 05 section 6 gives, in its order.
    /// </summary>
    public static IReadOnlyList<RecipientDeclaration> Default { get; } =
    [
        new(
            "mail server",
            RecipientCharacterisation.Processor,
            ["mailbox contents", "account identifiers"],
            Location: null,
            AgreementReference: null,
            Callback: false),
        new(
            "hosting provider",
            RecipientCharacterisation.Processor,
            ["all stored data"],
            Location: null,
            AgreementReference: null,
            Callback: false),
        new(
            "password screening",
            RecipientCharacterisation.Recipient,
            ["hash prefix"],
            HostingLocation.Outside,
            AgreementReference: null,
            Callback: false),
    ];
}
