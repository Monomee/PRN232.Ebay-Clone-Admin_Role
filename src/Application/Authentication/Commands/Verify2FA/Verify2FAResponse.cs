namespace EbayClone.Application.Authentication.Commands.Verify2FA;

public class Verify2FAResponse
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Role { get; set; }
}
