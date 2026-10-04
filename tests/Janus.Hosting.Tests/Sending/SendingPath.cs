using System;
using System.Security.Cryptography;
using Janus.Authentication.Alerting;
using Janus.Authentication.Sending;
using Janus.Authentication.Tests;
using Janus.Authentication.Tests.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Sending;

namespace Janus.Hosting.Tests.Sending;

/// <summary>
/// The sending path as the library puts it together, over the fakes a test reads: the
/// admission, the governed send, the publisher that carries what was admitted, and the
/// handler it carries through, the shipped one unless a test stands in its own.
/// </summary>
/// <param name="configuration">Where the restrictions, the languages and the schedule are read.</param>
/// <param name="ledger">Where what was admitted is counted.</param>
/// <param name="outbox">Where an admitted message waits.</param>
/// <param name="templates">Where the shipped handler finds its words.</param>
/// <param name="mail">What carries a mail.</param>
/// <param name="sms">What carries a text message, and answers the gateway's balance.</param>
/// <param name="balances">Where the gateway's balance is kept.</param>
/// <param name="work">The unit of work every part of the path runs in.</param>
/// <param name="events">Where the events and the raised alerts go.</param>
/// <param name="clock">The clock the deployment runs on.</param>
/// <param name="randomness">Where references and jitter are drawn from.</param>
internal sealed class SendingPath(
    IConfigurationStore configuration,
    SendLedgerInMemory ledger,
    SendOutboxInMemory outbox,
    IMessageTemplates templates,
    IMailTransport mail,
    SmsTransportInMemory sms,
    SmsBalanceLedgerInMemory balances,
    UnitOfWorkInMemory work,
    EventsInMemory events,
    TimeProvider clock,
    RandomNumberGenerator randomness)
{
    /// <summary>
    /// The host-registered key suppliers, none unless a test names some.
    /// </summary>
    public RestrictionKeySuppliers Suppliers { get; set; } = RestrictionKeySuppliers.None;

    /// <summary>
    /// What is known about a number, considered before a restricted factor goes to it.
    /// </summary>
    public PhoneSignals Signals { get; set; } = Considered.Nothing(work, clock);

    /// <summary>
    /// The handler a deployment registered in the shipped one's place, where a test
    /// stands one in.
    /// </summary>
    public INotificationHandler? Replaced { get; set; }

    /// <summary>
    /// Where a spent retry budget's alert goes, the events unless a test stands in a
    /// channel that refuses.
    /// </summary>
    public IAlertChannels Alerts { get; set; } = events;

    /// <summary>
    /// What judges and counts a send.
    /// </summary>
    public SendAdmission Admission =>
        new(configuration, ledger, Suppliers, new SmsBalance(configuration, sms, balances, work, events, clock));

    /// <summary>
    /// What carries one admitted message.
    /// </summary>
    public INotificationHandler Handler =>
        Replaced ?? new NotificationHandler(configuration, templates, mail, sms);

    /// <summary>
    /// What carries what was admitted, after the commit and in its passes.
    /// </summary>
    public SendPublisher Publisher =>
        new(
            outbox,
            Admission,
            Handler,
            configuration,
            Alerts,
            work,
            clock,
            randomness);

    /// <summary>
    /// The governed send every area undertakes a message through.
    /// </summary>
    public GovernedSend Send =>
        new(Admission, outbox, Publisher, Signals, configuration, work, clock, randomness);
}
