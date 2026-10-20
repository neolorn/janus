using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Janus.Core;
using Janus.Privacy.Erasures;

namespace Janus.Privacy;

/// <summary>
/// What the deployment declared read against what it registered: every subject-event
/// subscriber and purpose handler answers to a name of its own, every sensitive resource
/// type has a subscriber that covers it, every type holding personal data an export must
/// carry has an export contributor that covers it, and every purpose resting on consent or
/// on an objectable basis has a handler that names it.
/// </summary>
/// <param name="declaration">What the host declared about its own domain.</param>
/// <param name="subscribers">The subject-event subscribers the host registered.</param>
/// <param name="handlers">The purpose handlers the host registered.</param>
/// <param name="contributors">The export contributors the host registered.</param>
/// <remarks>
/// Implements PRIV-RIGHT-005b, PRIV-RIGHT-003, PRIV-RIGHT-001a, PRIV-CONS-008, IDN-LIFE-003a,
/// DR-016 and LIB-HOST-001.
/// Adding a subscriber requires no library change; leaving one out stops the
/// deployment rather than the erasure that would half happen.
/// </remarks>
internal sealed class HandlerCoverage(
    AuthorizationDeclaration declaration,
    IEnumerable<ISubjectEventSubscriber> subscribers,
    IEnumerable<IPurposeHandler> handlers,
    IEnumerable<IExportContributor> contributors)
{
    /// <summary>
    /// Reads the declaration against the registrations.
    /// </summary>
    /// <returns>Nothing, or the first omission, named.</returns>
    public Result Validate()
    {
        // A confirmation is recorded under the subscriber's or the handler's name, so two
        // under one name, or one under the erasure ledger's, would each read the other's
        // confirmation as its own and a delivery would close half done (D-203).
        var names = new HashSet<string>([ErasureLedgerSubscriber.Called], StringComparer.Ordinal);

        foreach (string name in subscribers
            .Select(subscriber => subscriber.Name)
            .Concat(handlers.Select(handler => handler.Name)))
        {
            if (!names.Add(name))
            {
                return Result.Failure(Error.From(
                    ErrorCodes.StartupSubscriberName,
                    "handler",
                    JsonSerializer.SerializeToElement(name)));
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

        // PRIV-RIGHT-003 (D-203): what a host holds under a subject's key or in a
        // sensitive category reaches the subject's export only through a contributor.
        var exported = new HashSet<ResourceType>(
            contributors.SelectMany(contributor => contributor.Covers));

        foreach (ResourceTypeDeclaration type in Personal())
        {
            if (!exported.Contains(type.Name))
            {
                return Missing(type.Name.ToString());
            }
        }

        var named = new HashSet<string>(
            handlers.SelectMany(handler => handler.Purposes),
            StringComparer.Ordinal);

        foreach (string purpose in Handled())
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

    private IEnumerable<ResourceTypeDeclaration> Personal() =>
        declaration.ResourceTypes
            .Where(type => type.EncryptedFields.Count > 0 || type.SensitiveCategories.Count > 0)
            .OrderBy(type => type.Name.ToString(), StringComparer.Ordinal);

    // PRIV-RIGHT-001a AC3, D-203: a purpose resting on consent has a handler as one on
    // an objectable basis does, since a withdrawal erases what the host held solely for
    // it.
    private IEnumerable<string> Handled()
    {
        var bases = new HashSet<string>(
            declaration.LawfulBases
                .Where(basis => basis.IsConsent || basis.IsObjectable)
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
