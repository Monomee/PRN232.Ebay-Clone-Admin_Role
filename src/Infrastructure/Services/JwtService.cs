using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace EbayClone.Infrastructure.Services;

public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;

    public JwtService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(User user, IEnumerable<string>? permissions = null)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]!);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), // lets logout revoke this token
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email!),
            new Claim(ClaimTypes.Name, user.Email!),
            new Claim(ClaimTypes.Role, user.Role!),
            new Claim("twoFactorEnabled", user.TwoFactorEnabled.ToString().ToLower())
        };
        claims.AddRange((permissions ?? []).Select(p => new Claim(AdminPermissions.ClaimType, p)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(key),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.Add(TokenLifetime),
            signingCredentials: credentials);
          
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateTwoFactorChallenge(int userId)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: ChallengeAudience,
            claims: [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            expires: DateTime.UtcNow.Add(TwoFactorChallengeLifetime),
            signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public int? ValidateTwoFactorChallenge(string? challenge)
    {
        if (string.IsNullOrEmpty(challenge)) return null;
        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(challenge, new TokenValidationParameters
            {
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidAudience = ChallengeAudience,
                IssuerSigningKey = SigningKey,
                ClockSkew = TimeSpan.Zero
            }, out _);
            return int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            return null;
        }
    }

    public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(60);
    public static readonly TimeSpan TwoFactorChallengeLifetime = TimeSpan.FromMinutes(5);

    // The login JWT validator only accepts Jwt:Audience, so a challenge can never authenticate an API call.
    private string ChallengeAudience => _configuration["Jwt:Audience"] + ":2fa-challenge";

    private SymmetricSecurityKey SigningKey => new(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
}
