using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Hosting.Privacy;

/// <summary>
/// The generated records of processing as chapter 09 section 8 gives them.
/// </summary>
/// <param name="GeneratedAt">When it was generated.</param>
/// <param name="HostingEnvironment">What the deployment runs on.</param>
/// <param name="HostingLocation">Where that is.</param>
/// <param name="CrossBorderBasis">What a transfer out of the country rests on.</param>
/// <param name="DataOwner">Who owns the data, as a person stated it.</param>
/// <param name="OrganisationalSecurityMeasures">The measures that are not the software's.</param>
/// <param name="AssessmentLinks">The assessments, as a person stated them.</param>
/// <param name="Records">One row a purpose.</param>
/// <param name="Recipients">Everyone the data reaches.</param>
/// <param name="Flags">What a person still has to supply.</param>
/// <remarks>Implements PRIV-PRIN-002, PRIV-ROPA-001, PRIV-ROPA-002 and PRIV-ROPA-003.</remarks>
internal sealed record ProcessingRegisterView(
    DateTimeOffset GeneratedAt,
    string HostingEnvironment,
    HostingLocation HostingLocation,
    string? CrossBorderBasis,
    string? DataOwner,
    string? OrganisationalSecurityMeasures,
    IReadOnlyList<string> AssessmentLinks,
    IReadOnlyList<ProcessingRecordView> Records,
    IReadOnlyList<RecipientRecordView> Recipients,
    IReadOnlyList<RegisterFlagView> Flags)
{
    /// <summary>
    /// The register as the endpoint answers with it.
    /// </summary>
    /// <param name="register">What was generated.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The register is absent.</exception>
    public static ProcessingRegisterView Of(ProcessingRegister register)
    {
        ArgumentNullException.ThrowIfNull(register);

        return new ProcessingRegisterView(
            register.GeneratedAt,
            register.HostingEnvironment,
            register.HostingLocation,
            register.CrossBorderBasis,
            register.DataOwner,
            register.OrganisationalSecurityMeasures,
            register.AssessmentLinks,
            [.. register.Records.Select(Row)],
            [.. register.Recipients.Select(Reached)],
            [.. register.Flags.Select(flag => new RegisterFlagView(flag.Finding, flag.Subject))]);
    }

    private static ProcessingRecordView Row(ProcessingRecord record) =>
        new(
            record.Purpose,
            record.DataCategories,
            record.SubjectCategories,
            record.LawfulBasis,
            record.NonSensitive,
            record.Sensitive,
            record.Children,
            record.SensitiveCategories,
            record.Retention,
            record.Recipients,
            record.DisposalMeasures,
            record.RolesWithAccess,
            record.TechnicalSecurityMeasures,
            record.Assessment);

    private static RecipientRecordView Reached(RecipientRecord recipient) =>
        new(
            recipient.Name,
            recipient.Characterisation,
            recipient.DataReceived,
            recipient.Location,
            recipient.AgreementReference,
            recipient.CrossBorderBasis,
            recipient.Callback);
}
