using System.Net.Http;

namespace Janus.Testing.Tests;

/// <summary>
/// One request a browser made.
/// </summary>
/// <param name="Method">Its method.</param>
/// <param name="Path">The path it was made to.</param>
/// <param name="Body">What it carried, or nothing.</param>
internal sealed record RequestMade(HttpMethod Method, string Path, string Body);
