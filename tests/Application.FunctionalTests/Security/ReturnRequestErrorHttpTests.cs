using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using EbayClone.Domain.Entities;
using EbayClone.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Factory = EbayClone.Security.HttpTests.SensitiveLogHttpTests.Factory;

namespace EbayClone.Security.HttpTests;

/// Wrong-status return request actions must be 400 with the handler's reason in `title`
/// (the UI shows it), not 500 (z8rp3fd0pw, z8rp3fd0qf, z8rp3fd0qu, z8rp3fcz2n).
public class ReturnRequestErrorHttpTests
{
    private static Factory _factory = null!;

    [OneTimeSetUp] public void Start() => _factory = new Factory();
    [OneTimeTearDown] public void Stop() => _factory.Dispose();

    private static IEnumerable<TestCaseData> Rejected()
    {
        yield return new TestCaseData("Pending", "return-label", new { returnLabelUrl = "https://label.test/1" },
            "không ở trạng thái chờ mã vận đơn (Status: Pending)").SetName("ReturnLabel on Pending");
        yield return new TestCaseData("Approved", "approve", new { adminNote = "ok", resolutionAction = "KeepItem" },
            "không ở trạng thái hợp lệ để approve (Status: Approved)").SetName("Approve on Approved");
        yield return new TestCaseData("WaitingForReturnLabel", "reject", new { adminNote = "no" },
            "đã được xử lý trước đó (Status: WaitingForReturnLabel)").SetName("Reject on WaitingForReturnLabel");
        yield return new TestCaseData("Completed", "freeze", new { adminNote = "fraud" },
            "vì đã được xử lý xong (Status: Completed)").SetName("Freeze on Completed");
    }

    [TestCaseSource(nameof(Rejected))]
    public async Task WrongStatus_Returns400WithReason_AndDoesNotChangeRequest(string status, string action, object body, string reason)
    {
        int id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var request = new ReturnRequest { Status = status, Reason = "Damaged", CreatedAt = DateTime.UtcNow };
            db.ReturnRequests.Add(request);
            await db.SaveChangesAsync();
            id = request.Id;
        }

        var payload = JsonSerializer.SerializeToElement(body).EnumerateObject()
            .ToDictionary(p => p.Name, p => (object?)p.Value.ToString());
        payload["returnRequestId"] = id;
        // TestServer has no remote IP by default; the IP middleware needs one
        using var client = new HttpClient(_factory.Server.CreateHandler(c => c.Connection.RemoteIpAddress = IPAddress.Loopback))
            { BaseAddress = new Uri("https://localhost") };
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token());

        var response = await client.PostAsJsonAsync($"/api/adminreturnrequests/{id}/{action}", payload);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(problem.GetProperty("title").GetString(), Does.Contain(reason));

        using var check = _factory.Services.CreateScope();
        var saved = await check.ServiceProvider.GetRequiredService<ApplicationDbContext>().ReturnRequests.FindAsync(id);
        Assert.That(saved!.Status, Is.EqualTo(status));
    }

    private static string Token() => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
        "EbayCloneTest", "EbayCloneTest", [new Claim(ClaimTypes.NameIdentifier, "42"), new Claim(ClaimTypes.Role, "SuperAdmin")],
        expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SensitiveLogHttpTests.SigningKey)), SecurityAlgorithms.HmacSha256)));
}
