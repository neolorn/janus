using Janus.Core;

namespace Janus.Privacy.Tests;

/// <summary>
/// A deployment's declaration, spanning the bases the area branches on: a consent
/// basis over a sensitive type and over an ordinary one, a basis that is neither
/// consent nor objectable, and one that is objectable.
/// </summary>
/// <remarks>
/// CONV-TEST-005: the four purposes are what a privacy test needs to tell the paths
/// apart. Nothing here is the library's; a host declares its own.
/// </remarks>
internal static class Declaration
{
    /// <summary>
    /// A person's order, which is sensitive and carries purposes on three bases at
    /// once.
    /// </summary>
    internal sealed class Order
    {
        /// <summary>The order.</summary>
        public string Id { get; init; } = string.Empty;
    }

    /// <summary>
    /// A person's mailing preferences, which are not sensitive.
    /// </summary>
    internal sealed class Mailing
    {
        /// <summary>The preference set.</summary>
        public string Id { get; init; } = string.Empty;
    }

    /// <summary>
    /// The declaration itself, which the records of processing read the recipients
    /// and the resource types from.
    /// </summary>
    public static AuthorizationDeclaration Authorization { get; } = Declared().Build();

    /// <summary>
    /// The declaration a deployment has when it takes the shipped recipient rows as
    /// they come (PRIV-ROPA-002).
    /// </summary>
    public static AuthorizationDeclaration Reaching { get; } = Reached();

    /// <summary>
    /// What the deployment processes and on what basis.
    /// </summary>
    public static DeclaredProcessing Processing { get; } = DeclaredProcessing.Of(Authorization);

    /// <summary>
    /// The declaration as it stands, valid.
    /// </summary>
    /// <returns>The builder, so a test may change one thing before building.</returns>
    public static AuthorizationDeclarationBuilder Declared() =>
        new AuthorizationDeclarationBuilder()
            .LawfulBasis(new LawfulBasisDeclaration("agreement", true, true, false, false))
            .LawfulBasis(new LawfulBasisDeclaration("contract", false, false, false, false))
            .LawfulBasis(new LawfulBasisDeclaration("interest", false, false, true, true))
            .SensitiveCategory("financial")
            .Permission("order:read")
            .Resource<Order>("order", order => order
                .BelongsToOrganization()
                .Sensitive("financial")
                .Purpose("fulfilment", "contract", data: ["identity", "order"], subjects: ["customers"])
                .Purpose("recommendations", "agreement", data: ["order"], subjects: ["customers"])
                .Purpose(
                    "security",
                    "interest",
                    assessment: "The abuse controls are assessed annually.",
                    data: ["order"],
                    subjects: ["customers"]))
            .Resource<Mailing>("mailing", mailing => mailing
                .BelongsToOrganization()
                .Purpose("marketing", "agreement", data: ["identity"], subjects: ["customers"]));

    private static AuthorizationDeclaration Reached()
    {
        AuthorizationDeclarationBuilder builder = Declared();

        foreach (RecipientDeclaration row in ProviderRegister.Default)
        {
            _ = builder.Recipient(row);
        }

        return builder.Build();
    }
}
