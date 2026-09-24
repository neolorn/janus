using Janus.Core;

namespace Janus.Authentication.Alerting;

/// <summary>
/// One raised condition, kept until the alert channels have carried it.
/// </summary>
/// <param name="Id">What it is kept under.</param>
/// <param name="Raised">The condition as it was raised.</param>
/// <remarks>Implements OPS-ALERT-001 and CONV-DESIGN-002.</remarks>
internal sealed record RaisedAlert(RaisedAlertId Id, AlertRaised Raised);
