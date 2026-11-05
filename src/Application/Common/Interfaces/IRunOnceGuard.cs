namespace EbayClone.Application.Common.Interfaces;

/// Cross-pod "only one of us does this": true for the first caller of a key within ttl (Redis when configured).
public interface IRunOnceGuard
{
    Task<bool> TryUseOnceAsync(string key, TimeSpan ttl);
}
