using System.Collections.Generic;
using Janus.Core.Configuration;

namespace Janus.Core;

/// <summary>
/// The provider register of chapter 05 section 8, shipped as declarations a
/// deployment edits rather than writes: the rows are the kinds of provider a
/// deployment of this library has, and the agreement reference of each is the
/// deployment's own.
/// </summary>
/// <remarks>
/// Implements PRIV-ROPA-002. A host declares the rows it actually has, because a
/// generic library cannot know that a deployment takes payments or ships anything.
/// The three rows the library itself makes true (the hosting provider, and the mail
/// server and the screening service where it calls them) are applied by the records
/// of processing whether or not the deployment declares them. Every row here carries
/// no agreement reference and is flagged until the deployment gives it one.
/// </remarks>
public static class ProviderRegister
{
    /// <summary>
    /// The rows chapter 05 section 8 gives, in its order.
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
            "payment provider",
            RecipientCharacterisation.Processor,
            ["payment details", "amount", "order reference"],
            Location: null,
            AgreementReference: null,
            Callback: true),
        new(
            "shipping provider",
            RecipientCharacterisation.Processor,
            ["name", "phone", "address", "generic description", "amount"],
            Location: null,
            AgreementReference: null,
            Callback: true),
        new(
            "sms gateway",
            RecipientCharacterisation.Processor,
            ["phone number", "message text"],
            Location: null,
            AgreementReference: null,
            Callback: true),
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
        new(
            "developer",
            RecipientCharacterisation.Processor,
            ["all stored data"],
            Location: null,
            AgreementReference: null,
            Callback: false),
    ];
}
