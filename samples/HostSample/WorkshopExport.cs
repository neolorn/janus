using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HostSample;

/// <summary>
/// What the workshop holds about a customer, carried into the customer's export: their
/// repairs and their grip fittings, each field held under the customer's key read with
/// it (PRIV-RIGHT-003).
/// </summary>
/// <param name="scopes">Where each export's scope is made.</param>
internal sealed class WorkshopExport(IServiceScopeFactory scopes) : IExportContributor
{
    /// <summary>
    /// The section carrying the customer's repairs.
    /// </summary>
    public const string Repairs = "repairs";

    /// <summary>
    /// The section carrying the customer's grip fittings.
    /// </summary>
    public const string Fittings = "fittings";

    /// <inheritdoc/>
    public string Name => "workshop";

    /// <inheritdoc/>
    public IReadOnlyCollection<ResourceType> Covers { get; } = [WorkshopModel.RepairType, WorkshopModel.FittingType];

    /// <inheritdoc/>
    public async ValueTask<Result<IReadOnlyList<ExportSection>>> SectionsAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        AsyncServiceScope scope = scopes.CreateAsyncScope();

        await using (scope.ConfigureAwait(false))
        {
            WorkshopContext context = scope.ServiceProvider.GetRequiredService<WorkshopContext>();
            IPersonalFields fields = scope.ServiceProvider.GetRequiredService<IPersonalFields>();

            List<Repair> repairs = await context.Repairs
                .Where(repair => repair.Customer == subject)
                .OrderBy(repair => repair.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            List<Fitting> fittings = await context.Fittings
                .Where(fitting => fitting.Customer == subject)
                .OrderBy(fitting => fitting.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return await fields
                .WithSubjectAsync(subject, customer => Sections(repairs, fittings, customer), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static Result<IReadOnlyList<ExportSection>> Sections(
        List<Repair> repairs,
        List<Fitting> fittings,
        SubjectFields customer)
    {
        var repaired = new List<ExportRecord>(repairs.Count);
        var fitted = new List<ExportRecord>(fittings.Count);

        foreach (Repair repair in repairs)
        {
            Result<string> name = customer.Decrypt(WorkshopModel.RepairType, WorkshopModel.ContactName, repair.ContactName);

            if (name.Refusal() is Error unread)
            {
                return Result.Failure<IReadOnlyList<ExportSection>>(unread);
            }

            repaired.Add(new ExportRecord(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ticket"] = repair.Id,
                ["kite"] = repair.Kite,
                ["contact-name"] = name.Value(),
                ["follow-up"] = repair.FollowUp ? "true" : "false",
                ["released-at"] = repair.ReleasedAt?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
            }));
        }

        foreach (Fitting fitting in fittings)
        {
            Result<string> need = customer.Decrypt(WorkshopModel.FittingType, WorkshopModel.GripNeed, fitting.Need);

            if (need.Refusal() is Error unread)
            {
                return Result.Failure<IReadOnlyList<ExportSection>>(unread);
            }

            fitted.Add(new ExportRecord(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["repair"] = fitting.RepairId,
                ["need"] = need.Value(),
            }));
        }

        return Result.Success<IReadOnlyList<ExportSection>>(
        [
            new ExportSection(Repairs, repaired),
            new ExportSection(Fittings, fitted),
        ]);
    }
}
