using System;
using System.Collections.Generic;
using System.Text;

namespace EbayClone.Application.Authentication.Commands.Login;
public class LoginResponse
{
    public bool RequireTwoFactor { get; set; }
    public string? Token { get; set; }
    public int UserId { get; set; }
    public bool Success { get; set; }      // thêm field này
    public string? ErrorMessage { get; set; } // thêm field này
    public string? Role { get; set; } // thêm field này

    /// Wrong email or password (not a ban or an unverified email): counts towards the per-account lockout. Not sent to the client.
    [System.Text.Json.Serialization.JsonIgnore]
    public bool InvalidCredentials { get; set; }
}
