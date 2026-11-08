using System.Net;
using Factory = EbayClone.Security.HttpTests.SensitiveLogHttpTests.Factory;

namespace EbayClone.Security.HttpTests;

/// [AsParameters] ignores property initializers: `int PageNumber { get; init; } = 1` made ?pageNumber= mandatory, so the
/// summary cards (which send only filters) got a 500 on Orders and Audit Logs. Defaults now live on the constructors.
public class QueryBindingHttpTests
{
    [TestCase("/api/AuditLogs/summary")]
    [TestCase("/api/Orders/summary")]
    [TestCase("/api/AuditLogs")]
    [TestCase("/api/Orders")]
    [TestCase("/api/Users")]
    [TestCase("/api/Broadcasts")]
    [TestCase("/api/Disputes")]
    [TestCase("/api/Products/managed")]
    [TestCase("/api/Orders/summary?status=Completed&search=x")]
    public async Task ListAndSummaryEndpoints_WorkWithoutPagingParameters(string url)
    {
        using var factory = new Factory();
        factory.EnsureSessionUser(900, "SuperAdmin");
        using var client = new HttpClient(factory.Server.CreateHandler(c => c.Connection.RemoteIpAddress = IPAddress.Loopback))
            { BaseAddress = new Uri("https://localhost") };
        client.DefaultRequestHeaders.Authorization = new("Bearer", SecurityHardeningHttpTests.Sign(900, "SuperAdmin"));

        var response = await client.GetAsync(url);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
    }

    // A malformed query value is the caller's mistake: 400 with the reason, not a generic 500.
    [Test]
    public async Task MalformedQueryValue_Is400NotServerError()
    {
        using var factory = new Factory();
        factory.EnsureSessionUser(901, "SuperAdmin");
        using var client = new HttpClient(factory.Server.CreateHandler(c => c.Connection.RemoteIpAddress = IPAddress.Loopback))
            { BaseAddress = new Uri("https://localhost") };
        client.DefaultRequestHeaders.Authorization = new("Bearer", SecurityHardeningHttpTests.Sign(901, "SuperAdmin"));

        var response = await client.GetAsync("/api/Orders?pageNumber=abc");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }
}
