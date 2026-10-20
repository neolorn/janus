using System;

namespace HostSample;

/// <summary>
/// A kite a customer brings in for repair.
/// </summary>
/// <param name="Ticket">The ticket on the kite's tag.</param>
/// <param name="Kite">What the kite is.</param>
/// <param name="ContactName">The name to contact the customer by.</param>
/// <param name="Collector">The person named to collect the kite, or nothing where the customer collects it.</param>
public sealed record Booking(string Ticket, string Kite, string ContactName, Guid? Collector);
