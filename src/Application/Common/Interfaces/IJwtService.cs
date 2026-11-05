using System;
using System.Collections.Generic;
using System.Text;
using EbayClone.Domain.Entities;

namespace EbayClone.Application.Common.Interfaces;
public interface IJwtService
{
    string GenerateToken(User user, IEnumerable<string>? permissions = null);

    /// Short-lived proof that the password step passed; only verify-2fa accepts it (separate audience).
    string GenerateTwoFactorChallenge(int userId);

    /// UserId from a valid, unexpired challenge, otherwise null.
    int? ValidateTwoFactorChallenge(string? challenge);
}
