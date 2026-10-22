using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Testing;

/// <summary>
/// One request a person's browser makes at the library's endpoints, the body written
/// as JSON, and what came back, read as JSON.
/// </summary>
/// <remarks>
/// Implements LIB-TEST-003. An answer whose status is none of those the step expects is
/// a fault of the deployment the test stood up, so it is thrown, naming the method, the
/// route, the status and the code the answer carried, and never a value the request or
/// the answer held.
/// </remarks>
internal static class Exchange
{
    private const string MediaType = "application/json";

    /// <summary>
    /// Sends one request and reads its answer, which is one of the statuses expected.
    /// </summary>
    /// <param name="client">The person's browser.</param>
    /// <param name="method">The method.</param>
    /// <param name="route">The route, relative to where the browser profile is mounted.</param>
    /// <param name="body">What the request carries, or nothing.</param>
    /// <param name="expected">The statuses the step answers.</param>
    /// <param name="cancellationToken">Abandons the request.</param>
    /// <returns>The answer.</returns>
    /// <exception cref="InvalidOperationException">The answer is none of those expected.</exception>
    public static async Task<Answer> SendAsync(
        HttpClient client,
        HttpMethod method,
        string route,
        JsonObject? body,
        HttpStatusCode[] expected,
        CancellationToken cancellationToken)
    {
        Answer answer = await AnswerAsync(client, method, route, body, cancellationToken).ConfigureAwait(false);

        return expected.Contains(answer.Status)
            ? answer
            : throw Unexpected(method, route, answer);
    }

    /// <summary>
    /// Sends one request and reads whatever it answers.
    /// </summary>
    /// <param name="client">The person's browser.</param>
    /// <param name="method">The method.</param>
    /// <param name="route">The route, relative to where the browser profile is mounted.</param>
    /// <param name="body">What the request carries, or nothing.</param>
    /// <param name="cancellationToken">Abandons the request.</param>
    /// <returns>The answer.</returns>
    public static async Task<Answer> AnswerAsync(
        HttpClient client,
        HttpMethod method,
        string route,
        JsonObject? body,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, new Uri(route, UriKind.Relative));

        if (body is not null)
        {
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, MediaType);
        }

        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

        string text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (text.Length is 0)
        {
            return new Answer(response.StatusCode, default);
        }

        using var read = JsonDocument.Parse(text);

        return new Answer(response.StatusCode, read.RootElement.Clone());
    }

    /// <summary>
    /// The fault an answer no step expects is thrown as.
    /// </summary>
    /// <param name="method">The method.</param>
    /// <param name="route">The route.</param>
    /// <param name="answer">What came back.</param>
    /// <returns>The fault.</returns>
    public static InvalidOperationException Unexpected(HttpMethod method, string route, Answer answer) =>
        new(method + " " + route + " answered " + (int)answer.Status
            + (answer.Code is { } code ? " " + code : string.Empty) + ".");
}
