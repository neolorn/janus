using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.Extensions.DependencyInjection;

namespace HostSample;

/// <summary>
/// The workshop's job that tells everyone who may read a repair that it is ready. A host
/// runs it in the background, never while a request is served, as the job the workshop
/// declares, under a principal of the organization the repair belongs to (IDN-PRIN-001).
/// It asks the library who may read the repair rather than keeping its own list of who
/// holds a role, which would miss a deny and the access a customer's booking derives
/// (AUTHZ-GATE-007).
/// </summary>
public sealed class ReadyNotices
{
    /// <summary>
    /// The job's name, as the workshop declares it.
    /// </summary>
    public const string Name = "ready-notices";

    // Why the job acts, recorded with each decision the gate takes for it.
    private const string Reason = "Tells everyone who may read a repair that it is ready for collection.";

    private readonly IServiceScopeFactory _scopes;

    internal ReadyNotices(IServiceScopeFactory scopes) => _scopes = scopes;

    /// <summary>
    /// The context the job acts in for an organization.
    /// </summary>
    /// <param name="organization">The organization whose repairs the job reads.</param>
    /// <returns>The context.</returns>
    public static AccessContext ContextFor(OrganizationId organization) =>
        AccessContext.Of(SystemPrincipal.ForOrganization(Name, Reason, organization));

    /// <summary>
    /// The contact details of everyone who may read a repair, to write to each that it
    /// is ready.
    /// </summary>
    /// <param name="organization">The organization the repair belongs to.</param>
    /// <param name="ticket">The repair's ticket.</param>
    /// <param name="cancellationToken">Abandons the run.</param>
    /// <returns>Each account's contact details, by its subject, or the library's refusal.</returns>
    public async Task<Result<IReadOnlyDictionary<SubjectId, ContactDetail>>> RecipientsAsync(
        OrganizationId organization,
        string ticket,
        CancellationToken cancellationToken)
    {
        AsyncServiceScope scope = _scopes.CreateAsyncScope();

        await using (scope.ConfigureAwait(false))
        {
            IAccessGate gate = scope.ServiceProvider.GetRequiredService<IAccessGate>();
            IAccounts accounts = scope.ServiceProvider.GetRequiredService<IAccounts>();
            AccessContext job = ContextFor(organization);

            Result<AdmittedAccounts> admitted = await gate
                .WhoMayAsync(
                    job,
                    WorkshopModel.ReadRepair,
                    new ResourceReference(WorkshopModel.RepairType, ResourceId.Parse(ticket)),
                    cancellationToken)
                .ConfigureAwait(false);

            if (admitted.Refusal() is Error denied)
            {
                return Result.Failure<IReadOnlyDictionary<SubjectId, ContactDetail>>(denied);
            }

            var recipients = new Dictionary<SubjectId, ContactDetail>();

            foreach (SubjectId subject in admitted.Value().Subjects)
            {
                Result<ContactDetail> contact = await accounts
                    .ContactAsync(job, subject, organization, cancellationToken)
                    .ConfigureAwait(false);

                if (contact.Refusal() is Error refused)
                {
                    return Result.Failure<IReadOnlyDictionary<SubjectId, ContactDetail>>(refused);
                }

                recipients[subject] = contact.Value();
            }

            return Result.Success<IReadOnlyDictionary<SubjectId, ContactDetail>>(recipients);
        }
    }
}
