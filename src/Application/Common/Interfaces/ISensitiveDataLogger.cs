namespace EbayClone.Application.Common.Interfaces;

public interface ISensitiveDataLogger
{
    // Adds to the caller's unit of work; never commits independently.
    void Add(string action, string targetEntity, string? targetId, string classification, object payload);
}
