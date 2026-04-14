// File: SFE.Domain/Enums/TransferStatus.cs
namespace SFE.Domain.Enums;

public enum TransferStatus
{
    Draft,
    Pending,
    InTransit,
    Received,
    PartiallyReceived,
    Cancelled
}