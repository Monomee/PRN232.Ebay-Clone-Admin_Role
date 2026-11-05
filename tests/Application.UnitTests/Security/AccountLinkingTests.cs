using EbayClone.Application.Users.Risk;
using NUnit.Framework;
using T = EbayClone.Application.Users.Risk.AccountLinking.Types;

namespace EbayClone.Application.UnitTests.Security;

public class AccountLinkingTests
{
    [TestCase("N.Guyen+shop2@GMail.com", "nguyen@gmail.com")]
    [TestCase("nguyen@googlemail.com", "nguyen@gmail.com")]
    [TestCase("a.b+x@outlook.com", "a.b@outlook.com")] // dots only matter outside gmail
    [TestCase("not-an-email", null)]
    public void Email_SpellingsOfOneMailbox_Match(string input, string? expected) =>
        Assert.That(AccountLinking.NormalizeEmail(input), Is.EqualTo(expected));

    [Test]
    public void Phone_Bank_Address_Ip_Normalize()
    {
        Assert.That(AccountLinking.NormalizePhone("0912 345 678"), Is.EqualTo(AccountLinking.NormalizePhone("+84-912-345-678")));
        Assert.That(AccountLinking.NormalizeBankAccount("0011-2233-44"), Is.EqualTo("0011223344"));
        Assert.That(AccountLinking.NormalizeAddress("123 Lê Lợi, P. Bến Nghé, Q.1", "TP. Hồ Chí Minh"),
            Is.EqualTo(AccountLinking.NormalizeAddress("123 Le Loi Phuong Ben Nghe Quan 1", "Ho Chi Minh")));
        Assert.That(AccountLinking.NormalizeAddress("Lê Lợi", "Hồ Chí Minh"), Is.Null, "a street without a number links strangers");
        Assert.That(AccountLinking.NormalizeIp("192.168.1.5"), Is.Null, "private = our proxy, not the customer");
        Assert.That(AccountLinking.NormalizeIp("::ffff:8.8.8.8"), Is.EqualTo("8.8.8.8"));
        Assert.That(AccountLinking.NormalizeGeo(21.00004, 105.0), Is.EqualTo(AccountLinking.NormalizeGeo(21.0, 105.00002)));
    }

    [Test]
    public void Assess_StrongestLinkPerType_BannedWeighsMore()
    {
        var cafe = Enumerable.Range(1, 50).Select(id => new LinkedAccount(id, T.Ip, false));
        Assert.That(AccountLinking.Assess(cafe, "a@x.com").Decision, Is.EqualTo("Approve"), "shared IP alone never blocks");

        Assert.That(AccountLinking.Assess([new(9, T.Ip, true)], null).Decision, Is.EqualTo("Review"));
        Assert.That(AccountLinking.Assess([new(9, T.Bank, false)], null).Decision, Is.EqualTo("Review"));
        Assert.That(AccountLinking.Assess([new(9, T.Bank, true)], null).Decision, Is.EqualTo("Reject"));
        Assert.That(AccountLinking.Assess([new(9, T.Phone, false), new(9, T.Address, false)], "x@yopmail.com").Score, Is.EqualTo(30 + 15 + 20));
    }
}
