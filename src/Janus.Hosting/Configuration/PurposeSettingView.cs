using System;
using Janus.Core;

namespace Janus.Hosting.Configuration;

/// <summary>
/// One purpose a person may consent to or object to.
/// </summary>
/// <param name="Purpose">The purpose's name.</param>
/// <param name="Takes">Whether it takes a <c>consent</c> or an <c>objection</c>.</param>
/// <param name="ConsentKind">Which capture path a consent runs through, or nothing for an objection.</param>
/// <param name="Document">The legal document that governs it.</param>
/// <remarks>Implements PRIV-BASIS-003, PRIV-CONS-007 and D-224.</remarks>
internal sealed record PurposeSettingView(
    string Purpose,
    string Takes,
    ConsentKind? ConsentKind,
    string Document)
{
    private const string Consent = "consent";

    private const string Objection = "objection";

    private const string Notice = "privacy-notice";

    /// <summary>
    /// Whether a purpose is one the person decides: it rests on consent or on a basis
    /// the person may object to.
    /// </summary>
    /// <param name="purpose">The purpose.</param>
    /// <returns>Whether it is answered.</returns>
    /// <exception cref="ArgumentNullException">The purpose is absent.</exception>
    public static bool Decided(DeclaredPurpose purpose)
    {
        ArgumentNullException.ThrowIfNull(purpose);

        return purpose.Basis.IsConsent || purpose.Basis.IsObjectable;
    }

    /// <summary>
    /// Reads a purpose.
    /// </summary>
    /// <param name="purpose">A purpose <see cref="Decided"/> holds.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The purpose is absent.</exception>
    public static PurposeSettingView Of(DeclaredPurpose purpose)
    {
        ArgumentNullException.ThrowIfNull(purpose);

        return purpose.Basis.IsConsent
            ? new PurposeSettingView(purpose.Name, Consent, purpose.Consent, purpose.Document ?? Notice)
            : new PurposeSettingView(purpose.Name, Objection, ConsentKind: null, purpose.Document ?? Notice);
    }
}
