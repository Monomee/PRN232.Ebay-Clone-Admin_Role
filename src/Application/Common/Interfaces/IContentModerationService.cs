namespace EbayClone.Application.Common.Interfaces;

public interface IContentModerationService
{
    Task<(bool IsFlagged, string? Reason)> ModerateContentAsync(string content, CancellationToken cancellationToken = default);
}
