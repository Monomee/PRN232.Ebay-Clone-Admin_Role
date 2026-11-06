using System.Net;
using EbayClone.Application.Common.Interfaces;
using EbayClone.Domain.Entities;

namespace EbayClone.Application.Users.Risk;

/// Security events (bank account changed, sign-in from far away) reach the owner in-app and by email. Whoever took over
/// the account can read and dismiss the in-app copy, but not the owner's mailbox.
public static class SecurityAlert
{
    /// In-app notification; the caller saves.
    public static void Add(IApplicationDbContext db, User user, string title, string content, DateTime now) =>
        db.Notifications.Add(new Notification
        {
            UserId = user.Id, Title = title, Content = content, Type = "InApp", Status = "Sent", SentAt = now, CreatedAt = now,
        });

    /// Call after the change is saved: best effort, a mail failure (already logged by the email service) never undoes it.
    public static async Task<bool> EmailAsync(IEmailService? email, User user, string title, string content)
    {
        if (email == null || string.IsNullOrWhiteSpace(user.Email)) return false;
        try
        {
            await email.SendEmailAsync(user.Email, "[eBay Clone] " + title,
                $"<p>{WebUtility.HtmlEncode(content)}</p><p style=\"color:#666\">This is an automatic security alert.</p>");
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
