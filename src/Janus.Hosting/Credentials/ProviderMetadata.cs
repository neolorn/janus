using System.Collections.Generic;
using Microsoft.IdentityModel.Tokens;

namespace Janus.Hosting.Credentials;

/// <summary>
/// What a social provider publishes about the events it signs: its issuer and its keys.
/// </summary>
/// <param name="Issuer">The issuer its events name.</param>
/// <param name="Keys">The keys its events are signed with.</param>
/// <remarks>Implements IDN-LIFE-012a.</remarks>
internal sealed record ProviderMetadata(string Issuer, IReadOnlyList<SecurityKey> Keys);
