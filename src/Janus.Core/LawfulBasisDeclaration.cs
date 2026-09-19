namespace Janus.Core;

/// <summary>
/// One lawful basis a host declares at startup, with the properties the library
/// branches on. The library never reads the key: a jurisdiction with a different list
/// declares it and the library changes not at all.
/// </summary>
/// <param name="Key">The basis as the declaration and the generated records name it.</param>
/// <param name="IsConsent">Whether processing on it rests on consent.</param>
/// <param name="RequiresWrittenConsentForSensitive">
/// Whether sensitive data on this basis needs written consent.
/// </param>
/// <param name="RequiresAssessment">Whether a purpose on it carries an assessment.</param>
/// <param name="IsObjectable">Whether a subject may object to processing on it.</param>
/// <remarks>Implements PRIV-BASIS-001, chapter 10 section 5.7, AUTHZ-MODEL-003.</remarks>
public sealed record LawfulBasisDeclaration(
    string Key,
    bool IsConsent,
    bool RequiresWrittenConsentForSensitive,
    bool RequiresAssessment,
    bool IsObjectable);
