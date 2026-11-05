using System.Security.Claims;

using EbayClone.Application.Common.Interfaces;

namespace EbayClone.Web.Services;

public class CurrentUser : IUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? Id => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
    public string? Name => _httpContextAccessor.HttpContext?.User?.Identity?.Name;
    public bool IsSystem => _httpContextAccessor.HttpContext == null;
    public List<string>? Roles => _httpContextAccessor.HttpContext?.User?.FindAll(ClaimTypes.Role).Select(x => x.Value).ToList();

    // UseForwardedHeaders already resolved the client IP from trusted proxies; reading the raw header would trust anyone.
    public string? IpAddress => _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();
}
