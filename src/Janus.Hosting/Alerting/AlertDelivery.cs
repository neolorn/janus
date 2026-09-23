namespace Janus.Hosting.Alerting;

/// <summary>
/// What became of one alert: how many destinations took it on each channel, and
/// whether a channel that should have carried it could not.
/// </summary>
/// <param name="Email">How many addresses took it.</param>
/// <param name="Sms">How many numbers took it.</param>
/// <param name="SmsUnreachable">
/// Whether SMS was to carry it and carried it nowhere, which is the residual case of
/// a gateway account at zero: email carries alone and the gap is recorded.
/// </param>
/// <param name="Deduplicated">Whether an alert already stood for this condition.</param>
/// <remarks>Implements OPS-ALERT-002 and OPS-ALERT-003.</remarks>
internal sealed record AlertDelivery(int Email, int Sms, bool SmsUnreachable, bool Deduplicated);
