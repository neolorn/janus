using System;
using System.Collections.Frozen;
using System.IO;
using System.Text;
using System.Text.Json;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Hosting.Organizations;

/// <summary>
/// What a replacement of an organization's policy carries: the fields it overrides,
/// in the form chapter 10 section 4.1a writes the policy object, and the reason.
/// </summary>
/// <remarks>
/// Implements chapter 09 section 8a, D-143 and API-CONV-002. A member the policy object
/// does not have is refused rather than passed over, so a misspelt field never leaves
/// an organization under a looser policy than the one its administrator wrote.
/// </remarks>
internal static class OrganizationPolicyBody
{
    private const string Reason = "reason";

    private static readonly FrozenSet<string> Fields = new[]
    {
        "requiredAssurance",
        "loginFactors",
        "gates",
        "credentialRedundancy",
        "selfServiceRecovery",
        "emailDomains",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// Reads a replacement.
    /// </summary>
    /// <param name="body">The request body.</param>
    /// <param name="organization">The organization whose policy it replaces.</param>
    /// <returns>
    /// The override and the reason, or the refusal: <c>api.request.malformed</c> for a
    /// body that is no object or a member it does not know, naming the member, and
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

        string reason = string.Empty;
        using var buffer = new MemoryStream();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();

            foreach (JsonProperty member in body.EnumerateObject())
            {
                if (string.Equals(member.Name, Reason, StringComparison.Ordinal))
                {
                    if (member.Value.ValueKind is not JsonValueKind.String)
                    {
                        return Result.Failure<(PolicyOverride, string)>(Malformed(Reason));
                    }

                    reason = member.Value.GetString()!;

                    continue;
                }

                if (!Fields.Contains(member.Name))
                {
                    return Result.Failure<(PolicyOverride, string)>(Malformed(member.Name));
                }

                member.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        return Settings.OrganizationPolicy
            .Read(organization.ToString(), Encoding.UTF8.GetString(buffer.ToArray()))
            .Match(
                replacement => Result.Success((replacement, reason)),
                Result.Failure<(PolicyOverride, string)>);
    }

    private static Error Malformed(string member) =>
        Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement(member));
}
