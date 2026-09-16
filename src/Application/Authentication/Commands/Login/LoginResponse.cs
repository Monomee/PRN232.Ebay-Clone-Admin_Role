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
}
