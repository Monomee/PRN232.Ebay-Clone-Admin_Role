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
    public List<string>? Roles => _httpContextAccessor.HttpContext?.User?.FindAll(ClaimTypes.Role).Select(x => x.Value).ToList();

    public string? IpAddress =>
        _httpContextAccessor.HttpContext?.Request?.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',').FirstOrDefault()?.Trim()
        ?? _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();
}
