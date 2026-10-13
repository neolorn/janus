namespace Janus.Storage.Privacy.Bases;

/// <summary>
/// The <c>lawful_bases</c> row: one basis the host declared, with the properties the
/// library branches on.
/// </summary>
/// <remarks>Implements PRIV-BASIS-001 and CONV-ENUM-001.</remarks>
internal sealed class LawfulBasisRow
{
    /// <summary>The <c>key</c> column.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The <c>label</c> column.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The <c>is_consent</c> column.</summary>
    public bool IsConsent { get; set; }

    /// <summary>The <c>requires_written_consent_for_sensitive</c> column.</summary>
    public bool RequiresWrittenConsentForSensitive { get; set; }

    /// <summary>The <c>requires_assessment</c> column.</summary>
    public bool RequiresAssessment { get; set; }

    /// <summary>The <c>is_objectable</c> column.</summary>
    public bool IsObjectable { get; set; }
}
