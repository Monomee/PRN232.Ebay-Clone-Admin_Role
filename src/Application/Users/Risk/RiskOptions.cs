namespace EbayClone.Application.Users.Risk;

public class LinkWeight
{
    public int Active { get; set; }  // shared with an active account
    public int Banned { get; set; }  // shared with a banned/rejected account
}

/// Sign-up risk scoring, bound from the "Risk" configuration section; anything left out keeps the default below.
/// Tune with real data: lower ReviewThreshold sends more sign-ups to manual review, higher RejectThreshold rejects fewer.
public class RiskOptions
{
    public int ReviewThreshold { get; set; } = 30;
    public int RejectThreshold { get; set; } = 80;

    public Dictionary<string, LinkWeight> LinkWeights { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        [AccountLinking.Types.Cccd] = new() { Active = 80, Banned = 100 },   // same citizen: one account per CCCD
        [AccountLinking.Types.Bank] = new() { Active = 50, Banned = 100 },   // money goes to the same person
        [AccountLinking.Types.Email] = new() { Active = 40, Banned = 60 },   // same mailbox behind gmail dots / +tags
        [AccountLinking.Types.Phone] = new() { Active = 30, Banned = 60 },
        [AccountLinking.Types.Address] = new() { Active = 15, Banned = 40 },
        [AccountLinking.Types.Ip] = new() { Active = 15, Banned = 30 },      // NAT, cafés, mobile carriers: weak on its own
        [AccountLinking.Types.Geo] = new() { Active = 15, Banned = 20 },
        [AccountLinking.Types.Device] = new() { Active = 30, Banned = 60 },  // same browser; a shared family PC is innocent
    };

    public int DisposableEmailWeight { get; set; } = 20;
    public int DatacenterIpWeight { get; set; } = 20;
    public int NameMismatchWeight { get; set; } = 40;
    /// Account farming from one network with private windows (a fresh device cookie each time): this many earlier
    /// sign-ups from the same public IP within SignupBurstWindow adds SignupBurstWeight (default: manual review).
    public int SignupBurstAccounts { get; set; } = 3;
    public int SignupBurstWeight { get; set; } = 30;

    /// Fails at startup instead of silently approving everyone (or rejecting everyone) because of a typo.
    public RiskOptions Validate()
    {
        var types = new[]
        {
            AccountLinking.Types.Cccd, AccountLinking.Types.Email, AccountLinking.Types.Phone, AccountLinking.Types.Bank,
            AccountLinking.Types.Address, AccountLinking.Types.Ip, AccountLinking.Types.Geo, AccountLinking.Types.Device,
        };
        var errors = new List<string>();
        if (ReviewThreshold <= 0 || RejectThreshold <= ReviewThreshold)
            errors.Add("Risk: 0 < ReviewThreshold < RejectThreshold is required.");
        foreach (var type in types)
            if (!LinkWeights.TryGetValue(type, out var w)) errors.Add($"Risk:LinkWeights:{type} is missing.");
            else if (w.Active < 0 || w.Banned < w.Active) errors.Add($"Risk:LinkWeights:{type}: 0 <= Active <= Banned is required.");
        if (DisposableEmailWeight < 0 || DatacenterIpWeight < 0 || NameMismatchWeight < 0 || SignupBurstWeight < 0 || SignupBurstAccounts < 1)
            errors.Add("Risk: signal weights must not be negative.");
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(" ", errors));
        return this;
    }
}
