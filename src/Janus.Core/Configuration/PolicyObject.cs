using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Janus.Core.Configuration;

/// <summary>
/// The policy object an organization's policy is replaced with: the fields of chapter
/// 10 section 4.1a an organization may state, without the domain lock.
/// </summary>
/// <remarks>
/// Implements chapter 09 section 8a, D-143 and D-248. A member the object does not have
/// is refused rather than passed over, so a misspelt field never leaves an organization
/// under a looser policy than the one written, whether the object came in a request or
/// from a file on the server.
/// </remarks>
internal static class PolicyObject
{
    private static readonly FrozenSet<string> Fields = new[]
    {
        "requiredAssurance",
        "loginFactors",
        "gates",
        "credentialRedundancy",
        "selfServiceRecovery",
        "photos",
        "grammaticalGender",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// The fields an object states, written as one object for the policy key to read.
    /// </summary>
    /// <param name="body">The object.</param>
    /// <param name="carried">The members beside the fields that the caller reads itself, passed over here.</param>
    /// <returns>
    /// The fields as one object, or <c>api.request.malformed</c> naming the first member
    /// the object does not have, <c>emailDomains</c> included.
    /// </returns>
    /// <exception cref="ArgumentNullException">The carried members are absent.</exception>
    public static Result<string> Written(JsonElement body, IReadOnlySet<string> carried)
    {
        ArgumentNullException.ThrowIfNull(carried);

        using var buffer = new MemoryStream();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();

            foreach (JsonProperty member in body.EnumerateObject())
            {
                if (carried.Contains(member.Name))
                {
                    continue;
                }

                if (!Fields.Contains(member.Name))
                {
                    return Result.Failure<string>(
                        Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement(member.Name)));
                }

                member.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        return Result.Success(Encoding.UTF8.GetString(buffer.ToArray()));
    }
}
