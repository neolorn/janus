using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HostSample;

/// <summary>
/// The workshop's post room, which takes every message the library owes: it records the
/// message as a letter, with the values its places hold, for the courier to carry by mail
/// or by text in the workshop's own words (INT-SMS-006, CONV-CONTENT-001). A message
/// handed over again under the same reference is the letter already recorded, and is
/// recorded once.
/// </summary>
/// <param name="scopes">Where each hand-over's scope is made.</param>
/// <param name="time">The clock the workshop runs on.</param>
internal sealed class PostRoom(IServiceScopeFactory scopes, TimeProvider time) : INotificationHandler
{
    /// <inheritdoc/>
    public async ValueTask<Result> SendAsync(SendRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        string reference = request.Reference.Value;
        AsyncServiceScope scope = scopes.CreateAsyncScope();

        await using (scope.ConfigureAwait(false))
        {
            WorkshopContext workshop = scope.ServiceProvider.GetRequiredService<WorkshopContext>();

            if (await workshop.Post.AnyAsync(letter => letter.Reference == reference, cancellationToken).ConfigureAwait(false))
            {
                return Result.Success();
            }

            _ = workshop.Post.Add(new Letter
            {
                Reference = reference,
                Kind = request.Kind.ToString(),
                Channel = request.Channel.ToString(),
                Destination = request.Destination,
                Languages = string.Join(',', request.Languages),
                TakenAt = time.GetUtcNow(),
            });
            workshop.Places.AddRange(request.Values.Select(place => new LetterPlace
            {
                Reference = reference,
                Name = place.Key,
                Value = place.Value,
            }));

            _ = await workshop.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }
}
