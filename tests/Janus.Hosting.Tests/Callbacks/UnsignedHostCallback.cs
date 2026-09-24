using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting.Callbacks;
using Microsoft.Extensions.Primitives;

namespace Janus.Hosting.Tests.Callbacks;

/// <summary>
/// A host's callback from a provider that cannot sign: the reference arrives in the
/// query string, and the provider's API answers whatever a test says it does.
/// </summary>
internal sealed class UnsignedHostCallback : IUnsignedCallback
{
    /// <inheritdoc/>
    public string Name => "provider-status";

    /// <inheritdoc/>
    public IReadOnlyCollection<IPNetwork> Sources { get; set; } = [];

    /// <summary>
    /// What the provider's API answers.
    /// </summary>
    public bool Confirms { get; set; } = true;

    /// <summary>
    /// How many times the provider's API was asked.
    /// </summary>
    public int Asked { get; private set; }

    /// <inheritdoc/>
    public Result<string> ReferenceOf(CallbackDelivery delivery) =>
        delivery.Query.TryGetValue("reference", out StringValues reference)
            ? Result.Success(reference.ToString())
            : Result.Failure<string>(Error.From(ErrorCodes.CallbackRejected));

    /// <inheritdoc/>
    public ValueTask<Result> ConfirmAsync(CallbackDelivery delivery, CancellationToken cancellationToken)
    {
        Asked++;

        return ValueTask.FromResult(Confirms
            ? Result.Success()
            : Result.Failure(Error.From(ErrorCodes.CallbackRejected)));
    }
}
