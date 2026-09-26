using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Janus.Core;
using Janus.Privacy.Erasures;

namespace Janus.Privacy;

/// <summary>
/// What the deployment declared read against what it registered: every subject-event
/// subscriber answers to a name of its own, every sensitive resource type has one that
/// covers it, and every objectable purpose has a handler that names it.
/// </summary>
/// <param name="declaration">What the host declared about its own domain.</param>
/// <param name="subscribers">The subject-event subscribers the host registered.</param>
/// <param name="handlers">The purpose handlers the host registered.</param>
/// <remarks>
/// Implements PRIV-RIGHT-005b, PRIV-RIGHT-001a, IDN-LIFE-003a, DR-016 and LIB-HOST-001.
/// Adding a subscriber requires no library change; leaving one out stops the
/// deployment rather than the erasure that would half happen.
/// </remarks>
internal sealed class HandlerCoverage(
    AuthorizationDeclaration declaration,
    IEnumerable<ISubjectEventSubscriber> subscribers,
    IEnumerable<IPurposeHandler> handlers)
{
    /// <summary>
    /// Reads the declaration against the registrations.
    /// </summary>
    /// <returns>Nothing, or the first omission, named.</returns>
    public Result Validate()
    {
        // A confirmation is recorded under the subscriber's name, so two subscribers
        // under one name, or one under the erasure ledger's, would each read the
        // other's confirmation as its own and an erasure would close half done.
        var names = new HashSet<string>([ErasureLedgerSubscriber.Called], StringComparer.Ordinal);

        foreach (ISubjectEventSubscriber subscriber in subscribers)
        {
            if (!names.Add(subscriber.Name))
            {
                return Result.Failure(Error.From(
                    ErrorCodes.StartupSubscriberName,
                    "handler",
                    JsonSerializer.SerializeToElement(subscriber.Name)));
            }
        }

        var covered = new HashSet<ResourceType>(
            subscribers.SelectMany(subscriber => subscriber.Covers));

        foreach (ResourceTypeDeclaration type in Sensitive())
        {
            if (!covered.Contains(type.Name))
            {
                return Missing(type.Name.ToString());
            }
        }

        var named = new HashSet<string>(
            handlers.SelectMany(handler => handler.Purposes),
            StringComparer.Ordinal);

        foreach (string purpose in Objectable())
        {
            if (!named.Contains(purpose))
            {
                return Missing(purpose);
            }
        }

        return Result.Success();
    }

    private static Result Missing(string handler) =>
        Result.Failure(Error.From(
            ErrorCodes.StartupDeclarationMissing,
            "handler",
            JsonSerializer.SerializeToElement(handler)));

    // The order is the declaration's own, so two omissions are reported in a
    // sequence an operator can work through rather than one the runtime chose.
    private IEnumerable<ResourceTypeDeclaration> Sensitive() =>
        declaration.ResourceTypes
            .Where(type => type.SensitiveCategories.Count > 0)
            .OrderBy(type => type.Name.ToString(), StringComparer.Ordinal);

    private IEnumerable<string> Objectable()
    {
        var bases = new HashSet<string>(
            declaration.LawfulBases
                .Where(basis => basis.IsObjectable)
                .Select(basis => basis.Key),
            StringComparer.Ordinal);

        return declaration.ResourceTypes
            .SelectMany(type => type.Purposes)
            .Where(purpose => bases.Contains(purpose.Basis))
            .Select(purpose => purpose.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);
    }
}
