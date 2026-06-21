namespace Domain.Tickets;

public enum TicketCategory
{
    AccountAccess,
    TechnicalIssue,
    BillingQuestion,
    FeatureRequest,
    BugReport,
    Other
}

public enum TicketPriority
{
    Urgent,
    High,
    Medium,
    Low
}

public enum TicketStatus
{
    New,
    InProgress,
    WaitingCustomer,
    Resolved,
    Closed
}

public enum TicketSource
{
    WebForm,
    Email,
    Api,
    Chat,
    Phone
}

public enum DeviceType
{
    Desktop,
    Mobile,
    Tablet
}
