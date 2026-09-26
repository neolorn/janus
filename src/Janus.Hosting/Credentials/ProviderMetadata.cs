using System;
using System.Collections.Generic;
using Microsoft.IdentityModel.Tokens;

namespace Janus.Hosting.Credentials;

/// <summary>
/// What a social provider publishes in one of its documents: its issuer and its keys,
/// and, in its discovery document, where a person signs in and how.
/// </summary>
/// <param name="Issuer">The issuer its tokens name.</param>
/// <param name="Keys">The keys its tokens are signed with.</param>
/// <param name="Authorization">
/// Where a person is sent to sign in, where the document names an HTTPS address for it.
/// </param>
/// <param name="Token">
/// Where a code is exchanged, where the document names an HTTPS address for it.
/// </param>
/// <param name="FormPost">Whether the provider returns the browser by a posted form.</param>
/// <param name="ProofKey">Whether the provider takes an S256 proof key.</param>
/// <remarks>Implements IDN-LIFE-012, IDN-LIFE-012a and REG-IDENT-008.</remarks>
internal sealed record ProviderMetadata(
    string Issuer,
    IReadOnlyList<SecurityKey> Keys,
    Uri? Authorization,
    Uri? Token,
    bool FormPost,
    bool ProofKey);
