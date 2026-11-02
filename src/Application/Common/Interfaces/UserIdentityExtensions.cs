namespace EbayClone.Application.Common.Interfaces;

public static class UserIdentityExtensions
{
    public static int GetRequiredActorId(this IUser user)
    {
        if (user.IsSystem || !int.TryParse(user.Id, out var id) || id <= 0)
            throw new UnauthorizedAccessException();
        return id;
    }
}
