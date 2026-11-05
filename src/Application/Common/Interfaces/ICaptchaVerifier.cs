namespace EbayClone.Application.Common.Interfaces;

/// Proves a human submitted the form (Cloudflare Turnstile). Separate interface because it calls out to the internet.
public interface ICaptchaVerifier
{
    Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken);
}
