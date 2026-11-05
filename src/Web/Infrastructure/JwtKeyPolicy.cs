using System.Security.Cryptography;
using System.Text;

namespace EbayClone.Web.Infrastructure;

/// Whoever knows the signing key can mint a token for any admin, so the app refuses to start with a key that is
/// too short or that has been published. Keys are compared by SHA-256 so the published values are not repeated here.
public static class JwtKeyPolicy
{
    public const int MinimumBytes = 32; // HMAC-SHA256

    // Committed to the public repository (k8s/secret.yaml, git history): refused in every environment.
    private static readonly HashSet<string> Leaked = ["2ac83d3e491b2604b230bc0199ca564eb9a22d7d3e2a6264d42023a78a011836"];

    // Sample values from appsettings.json.example and .env.example: fine on a dev box, never in Production.
    private static readonly HashSet<string> Samples =
    [
        "40e60eb9ab6e6b73b1589c432a41827249d81b27bbb170e87faf5bc9efc45f4a",
        "4331a9e2d8dc8004391eb054b918d3d636961bff881c5a824dd3e439ece59f59",
    ];

    private const string HowToFix = "Generate a new one, e.g. PowerShell: [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(64)) " +
                                    "or bash: openssl rand -base64 64, and set Jwt:Key (JWT_KEY in .env / the k8s secret).";

    public static void Ensure(string? key, IHostEnvironment environment)
    {
        if (string.IsNullOrEmpty(key) || Encoding.UTF8.GetByteCount(key) < MinimumBytes)
            throw new InvalidOperationException($"Jwt:Key must be at least {MinimumBytes} bytes. {HowToFix}");
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        if (Leaked.Contains(hash))
            throw new InvalidOperationException($"Jwt:Key is a key that was published in the repository: anyone can sign admin tokens with it. {HowToFix}");
        if (environment.IsProduction() && Samples.Contains(hash))
            throw new InvalidOperationException($"Jwt:Key is the sample value from the example config and cannot be used in Production. {HowToFix}");
    }
}
