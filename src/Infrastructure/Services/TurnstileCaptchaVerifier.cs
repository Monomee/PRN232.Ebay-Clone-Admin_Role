using System.Net.Http.Json;
using EbayClone.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EbayClone.Infrastructure.Services;

/// Cloudflare Turnstile server-side check. Test keys: secret 1x0000000000000000000000000000000AA always passes,
/// 2x0000000000000000000000000000000AA always fails.
public class TurnstileCaptchaVerifier(HttpClient http, IConfiguration config, IHostEnvironment env, ILogger<TurnstileCaptchaVerifier> logger)
    : ICaptchaVerifier
{
    private const string VerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

    public async Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken)
    {
        var secret = config["Captcha:TurnstileSecretKey"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            // Production without a key: no sign-ups rather than unprotected sign-ups.
            if (env.IsProduction())
            {
                logger.LogError("Captcha:TurnstileSecretKey is not configured: registration is refused.");
                return false;
            }
            logger.LogWarning("Captcha:TurnstileSecretKey is not configured: captcha skipped ({Environment}).", env.EnvironmentName);
            return true;
        }
        if (string.IsNullOrWhiteSpace(token)) return false;

        var form = new Dictionary<string, string> { ["secret"] = secret, ["response"] = token };
        if (!string.IsNullOrEmpty(remoteIp)) form["remoteip"] = remoteIp;
        try
        {
            using var response = await http.PostAsync(VerifyUrl, new FormUrlEncodedContent(form), cancellationToken);
            if (!response.IsSuccessStatusCode) return false;
            var result = await response.Content.ReadFromJsonAsync<SiteVerifyResult>(cancellationToken);
            return result?.Success == true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Turnstile verification failed: registration refused.");
            return false; // fail closed
        }
    }

    private sealed record SiteVerifyResult(bool Success);
}
