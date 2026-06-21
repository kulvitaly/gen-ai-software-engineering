using Domain.Tickets;

namespace Application.Tickets.Import;

internal static class TicketImportValueParser
{
    public static TicketCategory? ToCategory(string? value)
    {
        return TryParse(value, CategoryNames, out TicketCategory category) ? category : null;
    }

    public static TicketPriority? ToPriority(string? value)
    {
        return TryParse(value, PriorityNames, out TicketPriority priority) ? priority : null;
    }

    public static TicketStatus? ToStatus(string? value)
    {
        return TryParse(value, StatusNames, out TicketStatus status) ? status : null;
    }

    public static TicketSource? ToSource(string? value)
    {
        return TryParse(value, SourceNames, out TicketSource source) ? source : null;
    }

    public static DeviceType? ToDeviceType(string? value)
    {
        return TryParse(value, DeviceTypeNames, out DeviceType deviceType) ? deviceType : null;
    }

    public static IReadOnlyCollection<string> ToTags(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split([';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
    }

    private static bool TryParse<TEnum>(string? value, IReadOnlyDictionary<TEnum, string> names, out TEnum parsed)
        where TEnum : struct, Enum
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (var pair in names)
        {
            if (string.Equals(pair.Value, value.Trim(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(pair.Key.ToString(), value.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                parsed = pair.Key;
                return true;
            }
        }

        return false;
    }

    private static readonly IReadOnlyDictionary<TicketCategory, string> CategoryNames = new Dictionary<TicketCategory, string>
    {
        [TicketCategory.AccountAccess] = "account_access",
        [TicketCategory.TechnicalIssue] = "technical_issue",
        [TicketCategory.BillingQuestion] = "billing_question",
        [TicketCategory.FeatureRequest] = "feature_request",
        [TicketCategory.BugReport] = "bug_report",
        [TicketCategory.Other] = "other"
    };

    private static readonly IReadOnlyDictionary<TicketPriority, string> PriorityNames = new Dictionary<TicketPriority, string>
    {
        [TicketPriority.Urgent] = "urgent",
        [TicketPriority.High] = "high",
        [TicketPriority.Medium] = "medium",
        [TicketPriority.Low] = "low"
    };

    private static readonly IReadOnlyDictionary<TicketStatus, string> StatusNames = new Dictionary<TicketStatus, string>
    {
        [TicketStatus.New] = "new",
        [TicketStatus.InProgress] = "in_progress",
        [TicketStatus.WaitingCustomer] = "waiting_customer",
        [TicketStatus.Resolved] = "resolved",
        [TicketStatus.Closed] = "closed"
    };

    private static readonly IReadOnlyDictionary<TicketSource, string> SourceNames = new Dictionary<TicketSource, string>
    {
        [TicketSource.WebForm] = "web_form",
        [TicketSource.Email] = "email",
        [TicketSource.Api] = "api",
        [TicketSource.Chat] = "chat",
        [TicketSource.Phone] = "phone"
    };

    private static readonly IReadOnlyDictionary<DeviceType, string> DeviceTypeNames = new Dictionary<DeviceType, string>
    {
        [DeviceType.Desktop] = "desktop",
        [DeviceType.Mobile] = "mobile",
        [DeviceType.Tablet] = "tablet"
    };
}
