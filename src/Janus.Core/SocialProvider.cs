using System;
using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// A social provider the deployment signs people in through and takes security events
/// from: where the provider publishes what it signs with, the clients the deployment
/// holds at it, and what this application presents there.
/// </summary>
/// <param name="Provider">
/// Which provider, as the factor catalogue names it: <see cref="Factor.Google"/> or
/// <see cref="Factor.Apple"/>.
/// </param>
/// <param name="Metadata">
/// The provider's document naming the issuer and the key set its security events are
/// signed under: Google's Cross-Account Protection configuration, or Apple's discovery
/// document.
/// </param>
/// <param name="ClientIds">
/// The identifiers of the deployment's clients at the provider, which are the audiences
/// an event for this deployment names. The first is the client this application signs
/// people in as.
/// </param>
/// <param name="Configuration">
/// The provider's OpenID Connect discovery document, naming where a person is sent to
/// sign in, where the code is exchanged, and the issuer and key set its identity tokens
/// are signed under.
/// </param>
/// <param name="Return">
/// The address on this application the provider sends the person back to, registered
/// at the provider for the first client: the library's
/// <c>/callbacks/providers/{provider}/return</c> under the mount this application uses.
/// </param>
/// <param name="Secret">
/// What this application presents at the provider's token endpoint for the first
/// client, as its UTF-8 bytes, read from the secrets manager and never from
/// configuration: the client secret Google issued, or the signed client secret Apple
/// requires, which the deployment renews before it lapses.
/// </param>
/// <remarks>
/// Implements IDN-LIFE-012, IDN-LIFE-012a, REG-IDENT-008 and LIB-HOST-001. There is no
/// default: a deployment that declares nothing for a provider offers no sign-in through
/// it and refuses its events, because nothing it holds could verify either. Declaring
/// the same provider twice, naming a factor that is not a social provider, an address
/// that is not HTTPS, no client or no secret stops the deployment at startup.
/// </remarks>
public sealed record SocialProvider(
    Factor Provider,
    Uri Metadata,
    IReadOnlyList<string> ClientIds,
    Uri Configuration,
    Uri Return,
    [property: NeverLogged] ReadOnlyMemory<byte> Secret);
