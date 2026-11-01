using System;
using System.Collections.Frozen;
using System.Text.Json;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Hosting.Organizations;

/// <summary>
/// What a replacement of an organization's policy carries: the fields it overrides,
/// in the form chapter 10 section 4.1a writes the policy object, and the reason.
/// </summary>
/// <remarks>
/// Implements chapter 09 section 8a, D-143 and API-CONV-002. The fields are read as
/// the policy object reads them wherever it comes from (D-248), and the reason beside
/// them is the request's own.
/// </remarks>
internal static class OrganizationPolicyBody
{
    private const string Reason = "reason";

    private static readonly FrozenSet<string> Carried = new[] { Reason }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// Reads a replacement.
    /// </summary>
    /// <param name="body">The request body.</param>
    /// <param name="organization">The organization whose policy it replaces.</param>
    /// <returns>
    /// The override and the reason, empty where the body carries none, or the refusal:
    /// <c>api.request.malformed</c> for a body that is no object or a member it does not
    /// know, <c>emailDomains</c> included, naming the member, and
    /// <c>config.value.notallowed</c> for a field whose value the policy object does not
    /// take.
    /// </returns>
    public static Result<(PolicyOverride Replacement, string Reason)> Read(
        JsonElement body,
        OrganizationId organization)
    {
        if (body.ValueKind is not JsonValueKind.Object)
        {
            return Result.Failure<(PolicyOverride, string)>(Error.From(ErrorCodes.RequestMalformed));
        }

        Error? failure = null;

        string fields = PolicyObject.Written(body, Carried)
            .Match(written => written, error => Withheld(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<(PolicyOverride, string)>(failure);
        }

        string reason = string.Empty;

        if (body.TryGetProperty(Reason, out JsonElement stated))
        {
            if (stated.ValueKind is not JsonValueKind.String)
            {
                return Result.Failure<(PolicyOverride, string)>(Malformed(Reason));
            }

            reason = stated.GetString()!;
        }

        return Settings.OrganizationPolicy
            .Entered(organization.ToString(), fields)
            .Match(
                replacement => Result.Success((replacement, reason)),
                Result.Failure<(PolicyOverride, string)>);
    }

    private static string Withheld(Error error, ref Error? failure)
    {
        failure = error;

        return string.Empty;
    }

    private static Error Malformed(string member) =>
        Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement(member));
}
