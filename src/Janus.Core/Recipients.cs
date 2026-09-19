using System;
using System.Collections.Generic;
using System.Linq;

namespace Janus.Core;

/// <summary>
/// The recipients the records of processing list. The library ships the register of
/// chapter 5 section 8 as the set a deployment edits, and names those that carry no
/// agreement reference rather than letting them pass unnoticed.
/// </summary>
/// <remarks>
/// Implements PRIV-ROPA-002, INT-GEN-004 and LIB-HOST-001. Where the register leaves
/// a location to configuration, the shipped row says outside: a transfer that needs
/// a basis and does not is the error worth surfacing, not the reverse.
/// </remarks>
public sealed class Recipients
{
    private readonly IReadOnlyList<Recipient> _declared;

    private Recipients(IReadOnlyList<Recipient> declared) => _declared = declared;

    /// <summary>
    /// The register of chapter 5 section 8, which a deployment edits rather than
    /// writes from nothing.
    /// </summary>
    public static Recipients Shipped { get; } = new(
    [
        new Recipient(
            "mail server",
            RecipientCharacterisation.Processor,
            ["mailbox contents", "account identifiers"],
            RecipientLocation.Outside,
            AgreementReference: null,
            Callback: false),
        new Recipient(
            "payment provider",
            RecipientCharacterisation.Processor,
            ["payment details", "amount", "order reference"],
            RecipientLocation.Outside,
            AgreementReference: null,
            Callback: true),
        new Recipient(
            "shipping provider",
            RecipientCharacterisation.Processor,
            ["name", "phone", "address", "generic description", "amount"],
            RecipientLocation.Outside,
            AgreementReference: null,
            Callback: true),
        new Recipient(
            "sms gateway",
            RecipientCharacterisation.Processor,
            ["phone number", "message text"],
            RecipientLocation.Outside,
            AgreementReference: null,
            Callback: true),
        new Recipient(
            "hosting provider",
            RecipientCharacterisation.Processor,
            ["all stored data"],
            RecipientLocation.Outside,
            AgreementReference: null,
            Callback: false),
        new Recipient(
            "password screening",
            RecipientCharacterisation.Recipient,
            ["hash prefix"],
            RecipientLocation.Outside,
            AgreementReference: null,
            Callback: false),
        new Recipient(
            "developer",
            RecipientCharacterisation.Processor,
            ["all stored data"],
            RecipientLocation.Outside,
            AgreementReference: null,
            Callback: false),
    ]);

    /// <summary>
    /// Every recipient declared.
    /// </summary>
    public IReadOnlyList<Recipient> All => _declared;

    /// <summary>
    /// Those with no agreement reference, which the generated records flag.
    /// </summary>
    public IReadOnlyList<Recipient> Unreferenced =>
        [.. _declared.Where(recipient => string.IsNullOrWhiteSpace(recipient.AgreementReference))];

    /// <summary>
    /// Takes the declared recipients as one collection.
    /// </summary>
    /// <param name="declared">What the host declared.</param>
    /// <returns>The collection the records are generated from.</returns>
    /// <exception cref="ArgumentNullException">The sequence or a member is absent.</exception>
    /// <exception cref="ArgumentException">A name is empty or declared twice.</exception>
    public static Recipients Of(IEnumerable<Recipient> declared)
    {
        ArgumentNullException.ThrowIfNull(declared);

        var names = new HashSet<string>(StringComparer.Ordinal);
        var recipients = new List<Recipient>();

        foreach (Recipient recipient in declared)
        {
            ArgumentNullException.ThrowIfNull(recipient);

            if (string.IsNullOrWhiteSpace(recipient.Name))
            {
                throw new ArgumentException("A recipient is declared without a name.", nameof(declared));
            }

            if (!names.Add(recipient.Name))
            {
                throw new ArgumentException(
                    "The recipient '" + recipient.Name + "' is declared twice.",
                    nameof(declared));
            }

            recipients.Add(recipient);
        }

        return new Recipients(recipients);
    }
}
