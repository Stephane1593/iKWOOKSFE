namespace SFE.Domain.Enums;

public enum PaymentTransactionStatus
{
    Initiated,   // request accepted, nothing charged yet
    Processing,  // terminal/provider is working
    Approved,
    Declined,
    TimedOut,    // no answer in the window -> reconciliation needed
    Reconciled   // final truth established via status query
}