using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace HostSample;

/// <summary>
/// The workshop's own endpoints, mapped after the browser profile beside the library's.
/// Each needs a signed-in person save the catalogue, which answers anyone, and each that
/// touches a record asks the gate for the permission it acts under (BFF-HOST-001 to
/// BFF-HOST-003).
/// </summary>
internal static class WorkshopEndpoints
{
    // The workshop's own refusal of a booking past a workshop's capacity.
    private const string Full = "workshop.capacity.reached";

    // What a workshop's list asks the gate it may do to each repair.
    private static readonly Permission[] Listed =
    [
        WorkshopModel.ReadRepair,
        WorkshopModel.ReleaseRepair,
        WorkshopModel.CancelRepair,
    ];

    /// <summary>
    /// Maps them.
    /// </summary>
    /// <param name="endpoints">Where the workshop maps its endpoints.</param>
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        _ = endpoints.MapGet("/workshops", CatalogueAsync).AllowNoSession();
        _ = endpoints.MapPost("/workshops", OpenAsync);
        _ = endpoints.MapGet("/workshops/{workshop}/repairs", ListAsync);
        _ = endpoints.MapPost("/workshops/{workshop}/repairs", BookAsync);
        _ = endpoints.MapGet("/repairs/{ticket}", ReadAsync);
        _ = endpoints.MapDelete("/repairs/{ticket}", CancelAsync);
        _ = endpoints.MapPost("/repairs/{ticket}/release", ReleaseAsync);
        _ = endpoints.MapGet("/repairs/{ticket}/fittings", FittingsAsync);
        _ = endpoints.MapPost("/repairs/{ticket}/fittings", FitAsync);
    }

    // The catalogue of workshops, which anyone may read.
    private static async Task<IResult> CatalogueAsync(WorkshopContext context, CancellationToken cancellationToken)
    {
        List<CatalogueEntry> listed = await context.Workshops
            .OrderBy(workshop => workshop.Name)
            .Select(workshop => new CatalogueEntry(workshop.Id, workshop.Name, workshop.Capacity))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(listed);
    }

    // A workshop opened in an organization by one of its technicians, and registered
    // with the library in the same transaction.
    private static Task<IResult> OpenAsync(
        Opening opening,
        IRequestAccess access,
        IAccessGate gate,
        IUnitOfWork work,
        IResources resources,
        WorkshopContext context,
        CancellationToken cancellationToken) =>
        access.Current().Match(
            async held =>
            {
                var organization = new OrganizationId(opening.Organization);

                if ((await gate.RequireAsync(held.Context, WorkshopModel.OpenWorkshop, organization, cancellationToken)
                        .ConfigureAwait(false)).Refusal() is Error denied)
                {
                    return Answers.Refused(denied);
                }

                var workshop = new Workshop
                {
                    Id = Guid.NewGuid().ToString("n"),
                    Organization = opening.Organization,
                    Name = opening.Name,
                    Capacity = opening.Capacity,
                };

                if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false)).Refusal() is Error notBegun)
                {
                    return Answers.Refused(notBegun);
                }

                await context.Database.UseUnitOfWorkAsync(work, cancellationToken).ConfigureAwait(false);

                _ = context.Workshops.Add(workshop);
                _ = await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                if ((await resources
                        .RegisterAsync(
                            new ResourceRegistration(Of(WorkshopModel.WorkshopType, workshop.Id), organization, ContainedIn: null, Subject: null),
                            cancellationToken)
                        .ConfigureAwait(false)).Refusal() is Error unregistered)
                {
                    await work.RollbackAsync().ConfigureAwait(false);

                    return Answers.Refused(unregistered);
                }

                return Answers.Of(
                    await work.CommitAsync(cancellationToken).ConfigureAwait(false),
                    Results.Created(new Uri("/workshops/" + workshop.Id, UriKind.Relative), new Opened(workshop.Id)));
            },
            Refused);

    // A kite booked in by a signed-in customer: the repair, whose contact name is held
    // under the customer's key, and the facts its roles follow from, written and
    // registered in one transaction, which is rolled back whole where the booking takes
    // the workshop past its capacity.
    private static Task<IResult> BookAsync(
        string workshop,
        Booking booking,
        IRequestAccess access,
        IPersonalFields fields,
        IUnitOfWork work,
        IResources resources,
        WorkshopContext context,
        CancellationToken cancellationToken) =>
        access.Current().Match(
            async held =>
            {
                if (held.Context.Effective is not SubjectId customer || string.IsNullOrWhiteSpace(booking.Ticket))
                {
                    return Results.BadRequest();
                }

                Workshop? taking = await context.Workshops
                    .SingleOrDefaultAsync(row => row.Id == workshop, cancellationToken)
                    .ConfigureAwait(false);

                if (taking is null)
                {
                    return Results.NotFound();
                }

                Result<ReadOnlyMemory<byte>> sealedName = await fields
                    .WithSubjectAsync(
                        customer,
                        subject => subject.Encrypt(WorkshopModel.RepairType, WorkshopModel.ContactName, booking.ContactName),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (sealedName.Refusal() is Error unsealed)
                {
                    return Answers.Refused(unsealed);
                }

                if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false)).Refusal() is Error notBegun)
                {
                    return Answers.Refused(notBegun);
                }

                await context.Database.UseUnitOfWorkAsync(work, cancellationToken).ConfigureAwait(false);

                // CONV-DESIGN-003: the workshop's row is locked before anything is written,
                // so a second booking waits here until the first has committed or rolled
                // back, and then counts it. Locked after the writes, two bookings would
                // each hold the share lock their repair's foreign key takes and wait on
                // the other's.
                _ = await context.Database
                    .ExecuteSqlAsync($"SELECT 1 FROM workshop.workshops WHERE id = {taking.Id} FOR UPDATE", cancellationToken)
                    .ConfigureAwait(false);

                _ = context.Repairs.Add(new Repair
                {
                    Id = booking.Ticket,
                    WorkshopId = taking.Id,
                    Customer = customer,
                    Kite = booking.Kite,
                    ContactName = sealedName.Value().ToArray(),
                    FollowUp = true,
                });
                _ = context.Owners.Add(new RepairOwner { RepairId = booking.Ticket, Owner = customer });

                if (booking.Collector is Guid collector)
                {
                    _ = context.Collectors.Add(new RepairCollector { RepairId = booking.Ticket, Collector = new SubjectId(collector) });
                }

                _ = await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                if ((await resources
                        .RegisterAsync(
                            new ResourceRegistration(
                                Of(WorkshopModel.RepairType, booking.Ticket),
                                new OrganizationId(taking.Organization),
                                Of(WorkshopModel.WorkshopType, taking.Id),
                                customer),
                            cancellationToken)
                        .ConfigureAwait(false)).Refusal() is Error unregistered)
                {
                    await work.RollbackAsync().ConfigureAwait(false);

                    return Answers.Refused(unregistered);
                }

                // The count is read under the workshop's lock, so two bookings for the
                // last place cannot both stand.
                int holding = await context.Repairs
                    .CountAsync(row => row.WorkshopId == taking.Id && row.ReleasedAt == null, cancellationToken)
                    .ConfigureAwait(false);

                if (holding > taking.Capacity)
                {
                    await work.RollbackAsync().ConfigureAwait(false);

                    return Results.Conflict(new WorkshopRefusal(Full));
                }

                return Answers.Of(
                    await work.CommitAsync(cancellationToken).ConfigureAwait(false),
                    Results.Created(new Uri("/repairs/" + booking.Ticket, UriKind.Relative), null));
            },
            Refused);

    // A workshop's repairs, filtered by the gate in the workshop's own query, each with
    // the capabilities its reader holds on it, computed for the whole list at once.
    private static Task<IResult> ListAsync(
        string workshop,
        IRequestAccess access,
        IAccessGate gate,
        IPersonalFields fields,
        WorkshopContext context,
        CancellationToken cancellationToken) =>
        access.Current().Match(
            async held =>
            {
                Workshop? listing = await context.Workshops
                    .SingleOrDefaultAsync(row => row.Id == workshop, cancellationToken)
                    .ConfigureAwait(false);

                if (listing is null)
                {
                    return Results.NotFound();
                }

                FilterSources<Repair> sources = context.RepairSources();

                Result<Expression<Func<Repair, bool>>> filter = await gate
                    .FilterAsync(
                        held.Context,
                        WorkshopModel.ReadRepair,
                        WorkshopModel.RepairType,
                        new OrganizationId(listing.Organization),
                        sources,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (filter.Refusal() is Error denied)
                {
                    return Answers.Refused(denied);
                }

                List<Repair> repairs = await context.Repairs
                    .Where(filter.Value())
                    .Where(row => row.WorkshopId == listing.Id)
                    .OrderBy(row => row.Id)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (repairs.Count is 0)
                {
                    return Results.Ok(Array.Empty<RepairEntry>());
                }

                Result<IReadOnlyList<Capability>> capabilities = await gate
                    .CapabilitiesAsync(
                        held.Context,
                        WorkshopModel.RepairType,
                        [.. repairs.Select(row => ResourceId.Parse(row.Id))],
                        Listed,
                        sources,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (capabilities.Refusal() is Error unasked)
                {
                    return Answers.Refused(unasked);
                }

                var capable = capabilities.Value()
                    .ToDictionary(capability => capability.Resource, CapabilityFields.Of);

                Result<IReadOnlyDictionary<string, string?>> names = await fields
                    .WithSubjectsAsync(
                        [.. repairs.Select(row => row.Customer).Distinct()],
                        subjects => Contacts(repairs, subjects),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (names.Refusal() is Error unread)
                {
                    return Answers.Refused(unread);
                }

                IReadOnlyDictionary<string, string?> named = names.Value();

                return Results.Ok(repairs.Select(row =>
                {
                    CapabilityFields fieldsOf = capable[ResourceId.Parse(row.Id)];

                    return new RepairEntry(row.Id, row.Kite, named[row.Id], row.ReleasedAt, fieldsOf.Can, fieldsOf.Requires);
                }));
            },
            Refused);

    // One repair, read by whoever its gate admits.
    private static Task<IResult> ReadAsync(
        string ticket,
        IRequestAccess access,
        IAccessGate gate,
        IPersonalFields fields,
        WorkshopContext context,
        CancellationToken cancellationToken) =>
        access.Current().Match(
            async held =>
            {
                if (await RequiredAsync(gate, held, WorkshopModel.ReadRepair, ticket, context, cancellationToken)
                    .ConfigureAwait(false) is Error denied)
                {
                    return Answers.Refused(denied);
                }

                Repair? repair = await context.Repairs
                    .SingleOrDefaultAsync(row => row.Id == ticket, cancellationToken)
                    .ConfigureAwait(false);

                if (repair is null)
                {
                    return Results.NotFound();
                }

                Result<IReadOnlyDictionary<string, string?>> names = await fields
                    .WithSubjectsAsync([repair.Customer], subjects => Contacts([repair], subjects), cancellationToken)
                    .ConfigureAwait(false);

                if (names.Refusal() is Error unread)
                {
                    return Answers.Refused(unread);
                }

                string? contactName = names.Value()[repair.Id];

                return Results.Ok(new RepairView(
                    repair.Id,
                    repair.WorkshopId,
                    repair.Kite,
                    contactName,
                    contactName is null,
                    repair.FollowUp,
                    repair.ReleasedAt));
            },
            Refused);

    // A repaired kite released by a technician, which the gate binds to a step-up.
    private static Task<IResult> ReleaseAsync(
        string ticket,
        IRequestAccess access,
        IAccessGate gate,
        TimeProvider time,
        WorkshopContext context,
        CancellationToken cancellationToken) =>
        access.Current().Match(
            async held =>
            {
                if (await RequiredAsync(gate, held, WorkshopModel.ReleaseRepair, ticket, context, cancellationToken)
                    .ConfigureAwait(false) is Error denied)
                {
                    return Answers.Refused(denied);
                }

                DateTimeOffset now = time.GetUtcNow();

                int released = await context.Repairs
                    .Where(row => row.Id == ticket)
                    .ExecuteUpdateAsync(set => set.SetProperty(row => row.ReleasedAt, now), cancellationToken)
                    .ConfigureAwait(false);

                return released is 0 ? Results.NotFound() : Results.NoContent();
            },
            Refused);

    // A repair cancelled by its owner: its fittings, the facts its roles follow from and
    // the repair itself deleted, and their registrations removed, in one transaction.
    private static Task<IResult> CancelAsync(
        string ticket,
        IRequestAccess access,
        IAccessGate gate,
        IUnitOfWork work,
        IResources resources,
        WorkshopContext context,
        CancellationToken cancellationToken) =>
        access.Current().Match(
            async held =>
            {
                if (await RequiredAsync(gate, held, WorkshopModel.CancelRepair, ticket, context, cancellationToken)
                    .ConfigureAwait(false) is Error denied)
                {
                    return Answers.Refused(denied);
                }

                if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false)).Refusal() is Error notBegun)
                {
                    return Answers.Refused(notBegun);
                }

                await context.Database.UseUnitOfWorkAsync(work, cancellationToken).ConfigureAwait(false);

                List<string> fittings = await context.Fittings
                    .Where(row => row.RepairId == ticket)
                    .Select(row => row.Id)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                foreach (string fitting in fittings)
                {
                    if ((await resources.RemoveAsync(Of(WorkshopModel.FittingType, fitting), cancellationToken)
                            .ConfigureAwait(false)).Refusal() is Error kept)
                    {
                        await work.RollbackAsync().ConfigureAwait(false);

                        return Answers.Refused(kept);
                    }
                }

                _ = await context.Fittings.Where(row => row.RepairId == ticket).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
                _ = await context.Owners.Where(row => row.RepairId == ticket).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
                _ = await context.Collectors.Where(row => row.RepairId == ticket).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
                _ = await context.Repairs.Where(row => row.Id == ticket).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

                if ((await resources.RemoveAsync(Of(WorkshopModel.RepairType, ticket), cancellationToken)
                        .ConfigureAwait(false)).Refusal() is Error unremoved)
                {
                    await work.RollbackAsync().ConfigureAwait(false);

                    return Answers.Refused(unremoved);
                }

                return Answers.Of(await work.CommitAsync(cancellationToken).ConfigureAwait(false), Results.NoContent());
            },
            Refused);

    // A grip fitting the repair's owner asks for, made only with the owner's written
    // consent, which the gate asks for through the purpose the permission serves; the
    // need is held under the owner's key.
    private static Task<IResult> FitAsync(
        string ticket,
        FittingRequest request,
        IRequestAccess access,
        IAccessGate gate,
        IPersonalFields fields,
        IUnitOfWork work,
        IResources resources,
        WorkshopContext context,
        CancellationToken cancellationToken) =>
        access.Current().Match(
            async held =>
            {
                if (await RequiredAsync(gate, held, WorkshopModel.AddFitting, ticket, context, cancellationToken)
                    .ConfigureAwait(false) is Error denied)
                {
                    return Answers.Refused(denied);
                }

                var repair = await context.Repairs
                    .Where(row => row.Id == ticket)
                    .Join(context.Workshops, row => row.WorkshopId, workshop => workshop.Id, (row, workshop) => new { row.Customer, workshop.Organization })
                    .SingleOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (repair is null)
                {
                    return Results.NotFound();
                }

                Result<ReadOnlyMemory<byte>> sealedNeed = await fields
                    .WithSubjectAsync(
                        repair.Customer,
                        subject => subject.Encrypt(WorkshopModel.FittingType, WorkshopModel.GripNeed, request.Need),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (sealedNeed.Refusal() is Error unsealed)
                {
                    return Answers.Refused(unsealed);
                }

                var fitting = new Fitting
                {
                    Id = Guid.NewGuid().ToString("n"),
                    RepairId = ticket,
                    Customer = repair.Customer,
                    Need = sealedNeed.Value().ToArray(),
                };

                if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false)).Refusal() is Error notBegun)
                {
                    return Answers.Refused(notBegun);
                }

                await context.Database.UseUnitOfWorkAsync(work, cancellationToken).ConfigureAwait(false);

                _ = context.Fittings.Add(fitting);
                _ = await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                if ((await resources
                        .RegisterAsync(
                            new ResourceRegistration(
                                Of(WorkshopModel.FittingType, fitting.Id),
                                new OrganizationId(repair.Organization),
                                Of(WorkshopModel.RepairType, ticket),
                                repair.Customer),
                            cancellationToken)
                        .ConfigureAwait(false)).Refusal() is Error unregistered)
                {
                    await work.RollbackAsync().ConfigureAwait(false);

                    return Answers.Refused(unregistered);
                }

                return Answers.Of(
                    await work.CommitAsync(cancellationToken).ConfigureAwait(false),
                    Results.Created(new Uri("/repairs/" + ticket + "/fittings", UriKind.Relative), null));
            },
            Refused);

    // A repair's grip fittings, read only while the owner's written consent stands.
    private static Task<IResult> FittingsAsync(
        string ticket,
        IRequestAccess access,
        IAccessGate gate,
        IPersonalFields fields,
        WorkshopContext context,
        CancellationToken cancellationToken) =>
        access.Current().Match(
            async held =>
            {
                if (await RequiredAsync(gate, held, WorkshopModel.ReadFitting, ticket, context, cancellationToken)
                    .ConfigureAwait(false) is Error denied)
                {
                    return Answers.Refused(denied);
                }

                List<Fitting> fittings = await context.Fittings
                    .Where(row => row.RepairId == ticket)
                    .OrderBy(row => row.Id)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (fittings.Count is 0)
                {
                    return Results.Ok(Array.Empty<FittingView>());
                }

                Result<IReadOnlyList<FittingView>> read = await fields
                    .WithSubjectAsync(fittings[0].Customer, subject => Needs(fittings, subject), cancellationToken)
                    .ConfigureAwait(false);

                return Answers.Of(read, views => Results.Ok(views));
            },
            Refused);

    // Whether the gate admits the caller to act on one repair under a permission,
    // answered from the workshop's own data where a derivation reaches it.
    private static async ValueTask<Error?> RequiredAsync(
        IAccessGate gate,
        RequestAccess held,
        Permission permission,
        string ticket,
        WorkshopContext context,
        CancellationToken cancellationToken) =>
        (await gate
            .RequireAsync(held.Context, permission, Of(WorkshopModel.RepairType, ticket), context.RepairSources(), cancellationToken)
            .ConfigureAwait(false)).Refusal();

    // The contact name of each repair, read under its customer's key, or nothing where
    // that key is erased.
    private static Result<IReadOnlyDictionary<string, string?>> Contacts(
        IEnumerable<Repair> repairs,
        IReadOnlyDictionary<SubjectId, SubjectFields> subjects)
    {
        var named = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (Repair repair in repairs)
        {
            SubjectFields customer = subjects[repair.Customer];

            if (customer.Erased)
            {
                named[repair.Id] = null;

                continue;
            }

            Result<string> name = customer.Decrypt(WorkshopModel.RepairType, WorkshopModel.ContactName, repair.ContactName);

            if (name.Refusal() is Error unread)
            {
                return Result.Failure<IReadOnlyDictionary<string, string?>>(unread);
            }

            named[repair.Id] = name.Value();
        }

        return Result.Success<IReadOnlyDictionary<string, string?>>(named);
    }

    // What each fitting's customer's grip needs, read under the customer's key.
    private static Result<IReadOnlyList<FittingView>> Needs(List<Fitting> fittings, SubjectFields customer)
    {
        var read = new List<FittingView>(fittings.Count);

        foreach (Fitting fitting in fittings)
        {
            Result<string> need = customer.Decrypt(WorkshopModel.FittingType, WorkshopModel.GripNeed, fitting.Need);

            if (need.Refusal() is Error unread)
            {
                return Result.Failure<IReadOnlyList<FittingView>>(unread);
            }

            read.Add(new FittingView(fitting.Id, need.Value()));
        }

        return Result.Success<IReadOnlyList<FittingView>>(read);
    }

    private static ResourceReference Of(ResourceType type, string id) => new(type, ResourceId.Parse(id));

    private static Task<IResult> Refused(Error error) => Task.FromResult(Answers.Refused(error));
}
