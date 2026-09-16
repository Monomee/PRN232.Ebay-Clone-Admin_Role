namespace EbayClone.Domain.Constants;

public static class DisputeStatuses
{
    public const string Open = "Open";
    public const string AwaitingSellerResponse = "AwaitingSellerResponse";
    public const string Negotiating = "Negotiating";
    public const string Escalated = "Escalated";
    public const string AssignedToAdmin = "AssignedToAdmin";
    public const string UnderReview = "UnderReview";
    public const string Resolved = "Resolved";
    public const string Closed = "Closed";
}

public static class DisputeTypes
{
    public const string INR = "INR"; // Item Not Received
    public const string INAD = "INAD"; // Item Not as Described
    public const string Damaged = "Damaged";
    public const string Counterfeit = "Counterfeit";
    public const string Other = "Other";
}

public static class DisputePriorities
{
    public const string Critical = "Critical";
    public const string High = "High";
    public const string Medium = "Medium";
    public const string Low = "Low";
}

public static class DisputeWinners
{
    public const string Buyer = "Buyer";
    public const string Seller = "Seller";
    public const string Split = "Split";
}

public static class MessageTypes
{
    public const string Message = "Message";
    public const string Response = "Response";
    public const string Offer = "Offer";
    public const string Counter = "Counter";
    public const string Accept = "Accept";
    public const string Reject = "Reject";
    public const string Decline = "Decline";
    public const string Evidence = "Evidence";
    public const string Note = "Note";
    public const string SystemUpdate = "SystemUpdate";
}

public static class SenderTypes
{
    public const string Buyer = "Buyer";
    public const string Seller = "Seller";
    public const string Admin = "Admin";
    public const string System = "System";
}
