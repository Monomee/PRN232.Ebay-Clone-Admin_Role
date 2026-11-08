using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Factory = EbayClone.Security.HttpTests.SensitiveLogHttpTests.Factory;

// Outside the SQL suite's SetUpFixture: authorization is decided before any handler touches the DB.
namespace EbayClone.Security.HttpTests;

/// Regression for the [BUG] reports where unauthenticated callers or wrong roles reached admin APIs
/// (z8rp3fchrc, z8rp3fcxwr, z8rp3fcg8j). Each row is a call the tester made.
public class RbacHttpTests
{
    private static Factory _factory = null!;

    [OneTimeSetUp] public void Start() => _factory = new Factory();
    [OneTimeTearDown] public void Stop() => _factory.Dispose();

    private static IEnumerable<TestCaseData> DeniedCalls()
    {
        // No session at all
        foreach (var (m, p) in new[] {
            ("GET", "/api/products/managed"), ("POST", "/api/products/managed/1/resolve-violation"),
            ("GET", "/api/adminreturnrequests"), ("GET", "/api/adminreturnrequests/1"),
            ("POST", "/api/adminreturnrequests/1/approve"), ("POST", "/api/adminreturnrequests/1/adjudicate"),
            ("POST", "/api/adminreturnrequests/1/reject"), ("POST", "/api/adminreturnrequests/1/freeze"),
            ("POST", "/api/adminreturnrequests/1/return-label"),
            ("GET", "/api/Users/summary"), ("GET", "/api/Orders/summary"),
            ("GET", "/api/products/managed/summary"), ("GET", "/api/AuditLogs/summary"),
            ("GET", "/api/SystemAdmin/ip-blocks"), ("POST", "/api/SystemAdmin/ip-blocks") })
            yield return new TestCaseData(m, p, null, HttpStatusCode.Unauthorized).SetName($"Anonymous {m} {p}");

        // Authenticated, wrong role
        foreach (var (role, m, p) in new[] {
            ("Monitor", "POST", "/api/Disputes/2/assign"), ("Monitor", "POST", "/api/Disputes/2/resolve"),
            ("Monitor", "GET", "/api/Financials/settlement-orders"), ("Monitor", "GET", "/api/Financials/pending/4"),
            ("Support", "GET", "/api/Financials/settlement-orders"), ("Support", "GET", "/api/Financials/pending/4"),
            ("Seller", "GET", "/api/Dashboard/metrics"), ("Seller", "GET", "/api/Stats/dashboard"),
            ("Seller", "GET", "/api/Stats/reports/revenue"),
            ("Monitor", "GET", "/api/SystemAdmin/ip-blocks"), ("Seller", "POST", "/api/SystemAdmin/ip-blocks/1/lift") })
            yield return new TestCaseData(m, p, role, HttpStatusCode.Forbidden).SetName($"{role} {m} {p}");
    }

    [TestCaseSource(nameof(DeniedCalls))]
    public async Task AdminApi_RejectsCallerWithoutRequiredRole(string method, string path, string? role, HttpStatusCode expected)
    {
        Assert.That(await Send(method, path, role == null ? null : Token(role)), Is.EqualTo((int)expected));
    }

    // z8rp3fcgjj: a custom admin role is authorized by the permissions it grants, and only those.
    [Test]
    public async Task CustomRole_IsAuthorizedByGrantedPermissionsOnly()
    {
        var token = Token("QA Lead", "ManageOrders");
        Assert.That(await Send("GET", "/api/adminreturnrequests", token), Is.Not.EqualTo(401).And.Not.EqualTo(403));
        Assert.That(await Send("GET", "/api/Disputes", token), Is.EqualTo(403));
    }

    // z8rp3fcgjj: the UI reads the granted permissions from /me to open routes and menu items
    [Test]
    public async Task Me_ReturnsGrantedPermissions()
    {
        var response = await _factory.Server.SendAsync(context =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Loopback;
            context.Request.Scheme = "https";
            context.Request.Method = "GET";
            context.Request.Path = "/api/Auth/me";
            context.Request.Headers.Authorization = "Bearer " + Token("QA Lead", "ManageOrders", "ViewDashboard");
        });
        var body = await new StreamReader(response.Response.Body).ReadToEndAsync();
        Assert.That(response.Response.StatusCode, Is.EqualTo(200));
        Assert.That(body, Does.Contain("\"permissions\":[\"ManageOrders\",\"ViewDashboard\"]"));
    }

    private static async Task<int> Send(string method, string path, string? token)
    {
        var response = await _factory.Server.SendAsync(context =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Loopback;
            context.Request.Scheme = "https";
            context.Request.Method = method;
            context.Request.Path = path;
            if (token != null) context.Request.Headers.Authorization = "Bearer " + token;
        });
        return response.Response.StatusCode;
    }

    private static readonly List<string> SeededRoles = [];

    // One account per role: the server checks the token's role against the Users table on every request.
    private static string Token(string role, params string[] permissions)
    {
        if (!SeededRoles.Contains(role)) SeededRoles.Add(role);
        var userId = 1000 + SeededRoles.IndexOf(role);
        _factory.EnsureSessionUser(userId, role, permissions);
        return Sign(userId, role, permissions);
    }

    private static string Sign(int userId, string role, string[] permissions) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
        "EbayCloneTest", "EbayCloneTest", [new Claim("jti", Guid.NewGuid().ToString()), new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Role, role),
            .. permissions.Select(p => new Claim("permission", p))],
        expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SensitiveLogHttpTests.SigningKey)), SecurityAlgorithms.HmacSha256)));
}
