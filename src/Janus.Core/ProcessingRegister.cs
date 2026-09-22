using System;
using System.Collections.Generic;
using Janus.Core.Configuration;

namespace Janus.Core;

/// <summary>
/// The records of processing, generated from what the deployment declared and what it
/// is configured with. It is a query and not a document, so it cannot drift from what
/// the deployment actually does.
/// </summary>
/// <param name="GeneratedAt">When it was generated.</param>
/// <param name="HostingEnvironment">What the deployment runs on.</param>
/// <param name="HostingLocation">Where that is.</param>
/// <param name="CrossBorderBasis">
/// What a transfer out of the country rests on, where the deployment is outside it or
/// sends to a recipient that is.
/// </param>
/// <param name="DataOwner">Who owns the data, as a person stated it.</param>
/// <param name="OrganisationalSecurityMeasures">The measures that are not the software's.</param>
/// <param name="AssessmentLinks">The assessments, as a person stated them.</param>
/// <param name="Records">One row a purpose, in the order a reader expects.</param>
/// <param name="Recipients">Everyone the data reaches.</param>
/// <param name="Flags">What a person still has to supply.</param>
/// <remarks>
/// Implements PRIV-PRIN-002, PRIV-ROPA-001, PRIV-ROPA-002 and PRIV-ROPA-003.
/// </remarks>
public sealed record ProcessingRegister(
    DateTimeOffset GeneratedAt,
    string HostingEnvironment,
    HostingLocation HostingLocation,
    string? CrossBorderBasis,
    string? DataOwner,
    string? OrganisationalSecurityMeasures,
    IReadOnlyList<string> AssessmentLinks,
    IReadOnlyList<ProcessingRecord> Records,
    IReadOnlyList<RecipientRecord> Recipients,
    IReadOnlyList<RegisterFlag> Flags);
