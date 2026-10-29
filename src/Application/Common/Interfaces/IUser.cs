namespace EbayClone.Application.Common.Interfaces;

public interface IUser
{
    string? Id { get; }
    string? Name => null;
    List<string>? Roles { get; }
    bool IsSystem => false;

}
