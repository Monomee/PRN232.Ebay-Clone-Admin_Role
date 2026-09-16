namespace EbayClone.Domain.Constants;

public static class ReturnStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string AwaitingShipment = "AwaitingShipment";
    public const string InTransit = "InTransit";
    public const string Delivered = "Delivered";
    public const string Completed = "Completed";
    public const string Rejected = "Rejected";
    public const string Escalated = "Escalated";
    public const string WaitingForReturnLabel = "WaitingForReturnLabel";
    public const string ReturnLabelProvided = "ReturnLabelProvided";
    public const string Frozen = "Frozen";
}
