using System.Buffers.Text;
using System.Text.Json;
using Janus.Core;

namespace Janus.Hosting.Authentication;

/// <summary>
/// Reads the <c>AuthenticationResponseJSON</c> that <c>PublicKeyCredential.toJSON()</c>
/// gives for an assertion (WebAuthn Level 3 section 5.1), which the <c>value</c> of a
/// <c>passkey</c> or <c>securityKey</c> factor carries as a JSON object.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-014. The members the library does not read, the client
/// extension outputs among them, are passed over (D-197).
/// </remarks>
internal static class AuthenticationResponseJson
{
    /// <summary>
    /// Reads what the browser sent back.
    /// </summary>
    /// <param name="value">The object the factor's <c>value</c> carries.</param>
    /// <returns>
    /// The assertion, or nothing where a member the ceremony needs is absent, is not
    /// text or is not base64url.
    /// </returns>
    public static AuthenticatorAssertion? Read(JsonElement value)
    {
        if (value.ValueKind is not JsonValueKind.Object
            || !value.TryGetProperty("response", out JsonElement response)
            || response.ValueKind is not JsonValueKind.Object)
        {
            return null;
        }

        string? handle = null;

        if (response.TryGetProperty("userHandle", out JsonElement held)
            && held.ValueKind is not JsonValueKind.Null)
        {
            handle = Encoded(response, "userHandle");

            if (handle is null)
            {
                return null;
            }
        }

        return Encoded(value, "id") is string id
            && Encoded(value, "rawId") is string rawId
            && Text(value, "type") is string type
            && Encoded(response, "clientDataJSON") is string clientData
            && Encoded(response, "authenticatorData") is string authenticatorData
            && Encoded(response, "signature") is string signature
                ? new AuthenticatorAssertion(id, rawId, type, clientData, authenticatorData, signature, handle)
                : null;
    }

    private static string? Encoded(JsonElement within, string member) =>
        Text(within, member) is string text && Base64Url.IsValid(text) ? text : null;

    private static string? Text(JsonElement within, string member) =>
        within.TryGetProperty(member, out JsonElement read)
        && read.ValueKind is JsonValueKind.String
        && read.GetString() is { Length: > 0 } text
            ? text
            : null;
}
