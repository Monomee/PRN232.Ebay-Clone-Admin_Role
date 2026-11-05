namespace EbayClone.Application.Authentication.Commands.Verify2FA;

public class Verify2FAResponse
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Role { get; set; }

    /// TOTP time step the code matched; the endpoint uses it to refuse the same code twice. Not sent to the client.
    [System.Text.Json.Serialization.JsonIgnore]
    public long TimeStep { get; set; }
}
