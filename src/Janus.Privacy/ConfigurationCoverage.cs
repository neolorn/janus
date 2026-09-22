using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Privacy;

/// <summary>
/// What the deployment declared read against what it configured: every data category
/// a declared purpose is over has a retention period, and a deployment open to minors
/// has a written-consent basis to hold their data under.
/// </summary>
/// <param name="declaration">What the host declared about its own domain.</param>
/// <param name="processing">The purposes and the categories each is over.</param>
/// <param name="configuration">Where the keys are read.</param>
/// <remarks>
/// Implements PRIV-RET-001, PRIV-MINOR-001 and LIB-HOST-001. A category nobody named
/// a period for is data nobody ever deletes, which is found now rather than in the
/// year an auditor asks.
/// </remarks>
internal sealed class ConfigurationCoverage(
    AuthorizationDeclaration declaration,
    DeclaredProcessing processing,
    IConfigurationStore configuration)
{
    /// <summary>
    /// Reads the declaration against the configuration.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing, or the first omission, named.</returns>
    public async ValueTask<Result> ValidateAsync(CancellationToken cancellationToken)
    {
        foreach (string category in Categories())
        {
            Result<TimeSpan> kept = await configuration
                .ReadAsync(Settings.HostCategoryRetention, category, cancellationToken)
                .ConfigureAwait(false);

            if (kept.Match(_ => false, _ => true))
            {
                return Missing("key", Settings.HostCategoryRetention.For(category).ToString());
            }
        }

        AttributeRequirement affirmation = (await configuration
                .ReadAsync(Settings.RegistrationAdultAffirmation, cancellationToken)
                .ConfigureAwait(false))
            .Match(read => read, _ => Settings.RegistrationAdultAffirmation.Default);

        // PRIV-MINOR-001: a deployment that takes minors holds a child's data, which
        // is sensitive, and sensitive data on consent is held on written consent.
        return affirmation is AttributeRequirement.Off && !Written()
            ? Missing("key", "lawfulbasis.writtenconsent")
            : Result.Success();
    }

    private static Result Missing(string field, string named) =>
        Result.Failure(Error.From(
            ErrorCodes.StartupDeclarationMissing,
            field,
            JsonSerializer.SerializeToElement(named)));

    // The order is the declaration's own, so two omissions are reported in a sequence
    // an operator can work through rather than one the runtime chose.
    private IEnumerable<string> Categories() =>
        processing.Purposes
            .SelectMany(purpose => purpose.DataCategories)
            .Distinct(StringComparer.Ordinal);

    private bool Written() =>
        declaration.LawfulBases.Any(basis =>
            basis.IsConsent && basis.RequiresWrittenConsentForSensitive);
}
