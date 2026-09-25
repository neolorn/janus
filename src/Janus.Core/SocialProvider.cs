using System;
using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// A social provider whose security events the deployment takes: where the provider
/// publishes the issuer and the keys its events are signed with, and the clients the
/// deployment holds at it, which are the audiences an event for this deployment names.
/// </summary>
/// <param name="Provider">
/// Which provider, as the factor catalogue names it: <see cref="Factor.Google"/> or
/// <see cref="Factor.Apple"/>.
/// </param>
/// <param name="Metadata">
/// The provider's document naming its issuer and its key set: Google's Cross-Account
/// Protection configuration, or Apple's discovery document.
/// </param>
/// <param name="ClientIds">The identifiers of the deployment's clients at the provider.</param>
/// <remarks>
/// Implements IDN-LIFE-012a and LIB-HOST-001. There is no default: an event from a
/// provider the deployment declares nothing for is refused, because nothing it holds
/// could verify one. Declaring the same provider twice, naming a factor that is not a
/// social provider, an address that is not HTTPS or no client stops the deployment at
/// startup.
/// </remarks>
public sealed record SocialProvider(Factor Provider, Uri Metadata, IReadOnlyList<string> ClientIds);
