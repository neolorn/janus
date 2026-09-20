using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The recipients the records of processing list: the register the library ships,
/// and which of them the records flag for want of an agreement reference
/// (INT-GEN-004, PRIV-ROPA-002, chapter 5 section 8).
/// </summary>
[Trait("kind", "unit")]
public sealed class RecipientsTests
{
    private static readonly string[] Register =
    [
        "mail server",
        "payment provider",
        "shipping provider",
        "sms gateway",
        "hosting provider",
        "password screening",
        "developer",
    ];

    private static readonly string[] OneCategory = ["phone number"];

    /// <summary>
    /// INT-GEN-004 AC1: a provider declared without an agreement reference is
    /// flagged, and one declared with a reference is not.
    /// </summary>
    [Fact]
    public void INT_GEN_004_AC1_AProviderWithoutAnAgreementReferenceIsFlagged()
    {
        var declared = Recipients.Of(
        [
            Processor("gateway one", null),
            Processor("gateway two", "  "),
            Processor("gateway three", "DPA-2026-11"),
        ]);

        Assert.Equal(
            ["gateway one", "gateway two"],
            declared.Unreferenced.Select(recipient => recipient.Name));
    }

    /// <summary>
    /// INT-GEN-004 AC1: the shipped register names no agreement, so a deployment
    /// that has edited none of it is told about every row rather than none.
    /// </summary>
    [Fact]
    public void INT_GEN_004_AC1_TheShippedRegisterIsFlaggedUntilItIsEdited() =>
        Assert.Equal(Recipients.Shipped.All, Recipients.Shipped.Unreferenced);

    /// <summary>
    /// The register is the one chapter 5 section 8 gives, in its order, so a
    /// deployment edits a list rather than writing one (LIB-HOST-001).
    /// </summary>
    [Fact]
    public void Shipped_TheRegister_IsTheOneChapterFiveGives()
    {
        Assert.Equal(Register, Recipients.Shipped.All.Select(recipient => recipient.Name));

        Assert.Equal(
            ["payment provider", "shipping provider", "sms gateway"],
            Recipients.Shipped.All.Where(recipient => recipient.Callback).Select(recipient => recipient.Name));

        Assert.Equal(
            RecipientCharacterisation.Recipient,
            Recipients.Shipped.All.Single(recipient =>
                string.Equals(recipient.Name, "password screening", StringComparison.Ordinal)).Characterisation);
    }

    /// <summary>
    /// A recipient declared twice or declared without a name is a mistake in the
    /// host's declaration, refused where it is made (LIB-HOST-001).
    /// </summary>
    [Fact]
    public void Of_ANameMissingOrDeclaredTwice_IsRefused()
    {
        Assert.Throws<ArgumentException>(() => Recipients.Of(
            [Processor("gateway", null), Processor("gateway", "DPA-2026-11")]));

        Assert.Throws<ArgumentException>(() => Recipients.Of([Processor(" ", null)]));
        Assert.Throws<ArgumentNullException>(() => Recipients.Of(null!));
        Assert.Throws<ArgumentNullException>(() => Recipients.Of([null!]));
    }

    /// <summary>
    /// A deployment that declares its own recipients gets those and not the shipped
    /// register as well (LIB-HOST-001).
    /// </summary>
    [Fact]
    public void Of_TheDeclaredRecipients_AreTheOnesTheRecordsList()
    {
        IReadOnlyList<Recipient> declared = Recipients.Of([Processor("gateway", "DPA-2026-11")]).All;

        Assert.Equal("gateway", Assert.Single(declared).Name);
    }

    private static Recipient Processor(string name, string? agreement) =>
        new(
            name,
            RecipientCharacterisation.Processor,
            OneCategory,
            RecipientLocation.Outside,
            agreement,
            Callback: false);
}
